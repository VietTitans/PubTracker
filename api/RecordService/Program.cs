using System.Security.Claims;
using Npgsql;
using Pgvector.Npgsql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RecordService.Authentication;
using RecordService.DataAccess;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.ExternalSources;
using RecordService.ErrorHandling;
using RecordService.Workers;
using RecordService.BusinessLogic.UsersService;
using RecordService.BusinessLogic.SearchQueriesService;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.BusinessLogic.SourcesService;
using RecordService.BusinessLogic.DigestService;
using RecordService.BusinessLogic.ChatService;
using RecordService.DataAccess.Chat;
using RecordService.DataAccess.Embeddings;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("Database connection string is missing");
}

builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor();

// CORS - allows the React dev server (Vite, default port 5173) to call this API.
var webAppOrigin = builder.Configuration["Cors:WebAppOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(webAppOrigin).AllowAnyHeader().AllowAnyMethod());
});

// External Literature Sources - Register providers for factory pattern
var ncbiApiKey = builder.Configuration["Ncbi:ApiKey"];
var ncbiContactEmail = builder.Configuration["Ncbi:ContactEmail"];
builder.Services.AddSingleton<PubMedProvider>(sp =>
    new PubMedProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), ncbiApiKey, ncbiContactEmail));
builder.Services.AddSingleton<PedroProvider>();
builder.Services.AddSingleton<LiteratureSourceFactory>(serviceProvider =>
{
    var providers = new ILiteratureSourceProvider[]
    {
        serviceProvider.GetRequiredService<PubMedProvider>(),
        serviceProvider.GetRequiredService<PedroProvider>()
    };
    return new LiteratureSourceFactory(providers);
});

// Data Access Layer - Register interfaces to implementations
builder.Services.AddScoped(serviceProvider => 
    new UsersDataAccess(connectionString, serviceProvider.GetRequiredService<IHttpContextAccessor>()));
builder.Services.AddScoped<IUsersDataAccess>(sp => sp.GetRequiredService<UsersDataAccess>());
builder.Services.AddScoped<ISourcesDataAccess>(serviceProvider =>
    new SourcesDataAccess(connectionString));

builder.Services.AddScoped<ISearchQueriesDataAccess>(serviceProvider =>
    new SearchQueriesDataAccess(connectionString, serviceProvider.GetRequiredService<LiteratureSourceFactory>()));

// Chat / RAG - OpenAI embeds records and questions, Anthropic generates the grounded answer.
// RecordsDataAccess alone needs pgvector-aware type mapping, which requires building the
// NpgsqlDataSource through Pgvector's type resolver rather than handing it a bare connection
// string like every other DataAccess class here.
var openAiApiKey = builder.Configuration["OpenAi:ApiKey"];
var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"];

if (string.IsNullOrEmpty(openAiApiKey) || string.IsNullOrEmpty(anthropicApiKey))
{
    throw new Exception("OpenAI/Anthropic configuration is missing");
}

var vectorDataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
#pragma warning disable NPG9001 // AddTypeInfoResolverFactory is Npgsql's documented extension point for plugins like Pgvector, marked experimental pending a stable API
vectorDataSourceBuilder.AddTypeInfoResolverFactory(new VectorTypeInfoResolverFactory());
#pragma warning restore NPG9001
builder.Services.AddSingleton(vectorDataSourceBuilder.Build());

builder.Services.AddScoped<IEmbeddingClient>(sp =>
    new OpenAiEmbeddingClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        openAiApiKey,
        sp.GetRequiredService<ILogger<OpenAiEmbeddingClient>>()));
builder.Services.AddSingleton<IChatCompletionClient>(new AnthropicChatCompletionClient(anthropicApiKey));

builder.Services.AddScoped<IRecordsDataAccess>(serviceProvider =>
    new RecordsDataAccess(serviceProvider.GetRequiredService<NpgsqlDataSource>(), serviceProvider.GetRequiredService<IEmbeddingClient>()));

// Email - swappable behind IEmailSender; BrevoEmailSender is the only provider-specific piece
var emailApiKey = builder.Configuration["Email:ApiKey"];
var emailFromAddress = builder.Configuration["Email:FromAddress"];
var emailFromName = builder.Configuration["Email:FromName"];

if (string.IsNullOrEmpty(emailApiKey) || string.IsNullOrEmpty(emailFromAddress) || string.IsNullOrEmpty(emailFromName))
{
    throw new Exception("Email configuration is missing");
}

builder.Services.AddHttpClient();
builder.Services.AddScoped<IEmailSender>(serviceProvider =>
    new BrevoEmailSender(
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(),
        emailApiKey, emailFromAddress, emailFromName));

// Business Logic Layer - Register interfaces to implementations
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddScoped<ISearchQueriesService, SearchQueriesService>();
builder.Services.AddScoped<ISourcesService, SourcesService>();
builder.Services.AddScoped<IDigestService, DigestService>();
builder.Services.AddScoped<IRecordPollingService, RecordPollingService>();
builder.Services.AddScoped<IChatService, ChatService>();

// Background scheduler - polls every search query for new records on an interval
var pollIntervalHours = builder.Configuration.GetValue<double?>("Scheduler:PollIntervalHours") ?? 168;
builder.Services.AddHostedService(serviceProvider => new RecordPollingBackgroundService(
    serviceProvider.GetRequiredService<IServiceScopeFactory>(),
    serviceProvider.GetRequiredService<ILogger<RecordPollingBackgroundService>>(),
    TimeSpan.FromHours(pollIntervalHours)));

// Cross-cutting Concerns
builder.Services.AddScoped<IErrorHandler, DefaultErrorHandler>();

// Keycloak (real login). JWT `sub` is a Keycloak-generated UUID, not our internal
// users.id - GetOrProvisionByKeycloakSubAsync resolves/creates the matching users row on
// first successful validation, and the resulting internal id is what's written back onto
// the principal as ClaimTypes.NameIdentifier, so existing claims-reading controller code
// (int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))) keeps working unchanged.
//
// Authority vs MetadataAddress: Authority is the browser-facing URL and is what tokens are
// actually stamped with as `iss` (since Keycloak derives issuer from however the browser
// reached it), so it's what we validate the token's issuer against. Inside Docker, this API
// container can't reach Keycloak at that same browser-facing "localhost:8081" address
// (there's nothing listening on that port inside this container) - it needs the Docker
// network's service name instead. MetadataAddress lets the two diverge: when set (only in
// docker-compose), it's used purely for this container's own outbound signing-key fetch,
// while ValidIssuer stays pinned to the browser-facing Authority regardless.
var keycloakAuthority = builder.Configuration["Keycloak:Authority"] ?? "http://localhost:8081/realms/science-alerts-saas";
var keycloakMetadataAddress = builder.Configuration["Keycloak:MetadataAddress"];
var keycloakAudience = builder.Configuration["Keycloak:Audience"] ?? "science-alerts-api";

void ConfigureKeycloakBearer(JwtBearerOptions options)
{
    options.Authority = keycloakAuthority;
    if (!string.IsNullOrEmpty(keycloakMetadataAddress))
    {
        options.MetadataAddress = keycloakMetadataAddress;
    }
    options.Audience = keycloakAudience;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment(); // local dev Keycloak runs over plain HTTP
    options.MapInboundClaims = false; // keep raw JWT claim names ("sub", "email", ...) instead of the default ClaimTypes.* remapping
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = true,
        ValidateIssuer = true,
        ValidIssuer = keycloakAuthority,
        ClockSkew = TimeSpan.FromMinutes(5)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal!;
            var keycloakSub = principal.FindFirstValue("sub");
            if (string.IsNullOrEmpty(keycloakSub))
            {
                context.Fail("Token is missing a sub claim.");
                return;
            }

            var email = principal.FindFirstValue("email") ?? string.Empty;
            var name = principal.FindFirstValue("name") ?? email;
            var username = principal.FindFirstValue("preferred_username") ?? email;

            var usersService = context.HttpContext.RequestServices.GetRequiredService<IUsersService>();
            var user = await usersService.GetOrProvisionByKeycloakSubAsync(keycloakSub, email, name, username);

            ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        }
    };
}

// Dev-only placeholder auth so claims-reading endpoints (e.g. ClaimTypes.NameIdentifier)
// can be exercised via Swagger/Postman without a real Keycloak login. Never registered
// outside Development. Requests carrying a real "Authorization: Bearer <jwt>" header are
// still routed to the real Keycloak validation above; only requests without one fall back
// to the debug identity - see the "Smart" policy scheme below.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Smart";
            options.DefaultChallengeScheme = "Smart";
        })
        .AddPolicyScheme("Smart", "Bearer token or debug identity", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? JwtBearerDefaults.AuthenticationScheme
                    : DebugAuthenticationHandler.SchemeName;
            };
        })
        .AddScheme<AuthenticationSchemeOptions, DebugAuthenticationHandler>(DebugAuthenticationHandler.SchemeName, options => { })
        .AddJwtBearer(ConfigureKeycloakBearer);
}
else
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(ConfigureKeycloakBearer);
}

// Configure Authorization Policies for Roles
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => 
        policy.RequireRole("Admin"))
    .AddPolicy("UserOrAdmin", policy => 
        policy.RequireRole("User", "Admin"));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers(); 

app.Run();

public partial class Program { }
