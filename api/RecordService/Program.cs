using RecordService.DataAccess;

var builder = WebApplication.CreateBuilder(args);

var runMode = Environment.GetEnvironmentVariable("RUN_MODE");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("Database connection string is missing");
}

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
