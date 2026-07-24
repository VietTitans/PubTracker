using DotNetEnv;
using RecordService.DataAccess;

var envPath = Path.GetFullPath(
    Path.Combine(
        Directory.GetCurrentDirectory(),
        "../../docker/.env"
    )
);

Env.Load(envPath);

var builder = WebApplication.CreateBuilder(args);

var runMode = Environment.GetEnvironmentVariable("RUN_MODE");

string connectionString = runMode == "docker"
    ? Environment.GetEnvironmentVariable("DATABASE_URL_DOCKER")!
    : Environment.GetEnvironmentVariable("DATABASE_URL_LOCAL")!;

builder.Services.AddControllers();

builder.Services.AddScoped(ServiceProvider => new SourcesDataAccess(connectionString));

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
