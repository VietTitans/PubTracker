var builder = WebApplication.CreateBuilder(args);

var runMode = Environment.GetEnvironmentVariable("RUN_MODE");

string connectionString = runMode == "docker"
    ? Environment.GetEnvironmentVariable("DATABASE_URL_DOCKER")!
    : Environment.GetEnvironmentVariable("DATABASE_URL_LOCAL")!;

builder.Services.AddControllers();

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
