using RecordService.DataAccess;
using RecordService.BusinessLogic;
using RecordService.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("Database connection string is missing");
}

builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor();

// Data Access Layer - Register interfaces to implementations
builder.Services.AddScoped(serviceProvider => 
    new UsersDataAccess(connectionString, serviceProvider.GetRequiredService<IHttpContextAccessor>()));
builder.Services.AddScoped<IUsersDataAccess>(sp => sp.GetRequiredService<UsersDataAccess>());
builder.Services.AddScoped<ISourcesDataAccess>(serviceProvider =>
    new SourcesDataAccess(connectionString));

builder.Services.AddScoped<ISearchQueriesDataAccess, SearchQueriesDataAccess>();

// Business Logic Layer - Register interfaces to implementations
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddScoped<ISearchQueriesService, SearchQueriesService>();
builder.Services.AddScoped<ISourcesService, SourcesService>();

// Cross-cutting Concerns
builder.Services.AddScoped<IErrorHandler, DefaultErrorHandler>();

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

app.UseAuthorization();

app.MapControllers(); 

app.Run();
