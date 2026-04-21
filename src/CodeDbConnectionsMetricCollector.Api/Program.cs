using System.Text.Json;
using CodeDbConnectionsMetricCollector.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddHttpClient("github", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "code-db-connections-metric-collector");
});

builder.Services.AddScoped<IRepositoryAnalyzerService, RepositoryAnalyzerService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
