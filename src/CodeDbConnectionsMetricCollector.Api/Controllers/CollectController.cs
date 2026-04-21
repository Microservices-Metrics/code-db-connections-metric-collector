using Microsoft.AspNetCore.Mvc;
using CodeDbConnectionsMetricCollector.Api.Models;
using CodeDbConnectionsMetricCollector.Api.Services;

namespace CodeDbConnectionsMetricCollector.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class CollectController : ControllerBase
{
    private readonly IRepositoryAnalyzerService _analyzerService;
    private readonly ILogger<CollectController> _logger;

    public CollectController(IRepositoryAnalyzerService analyzerService, ILogger<CollectController> logger)
    {
        _analyzerService = analyzerService;
        _logger = logger;
    }

    /// <summary>
    /// Receives a repository URL and returns the number of database connections found in its source code.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CollectResponse>> Collect(
        [FromBody] CollectRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
            return BadRequest("repositoryUrl is required.");

        if (!Uri.TryCreate(request.RepositoryUrl, UriKind.Absolute, out _))
            return BadRequest("repositoryUrl must be a valid absolute URL.");

        _logger.LogInformation("Analyzing repository: {Url}", request.RepositoryUrl);

        var count = await _analyzerService.CountDatabaseConnectionsAsync(request.RepositoryUrl, cancellationToken);

        var response = new CollectResponse
        {
            Metric = new MetricInfo
            {
                Name = "Number of Database Connections",
                CollectorStrategy = "source code"
            },
            Measurement = new MeasurementInfo
            {
                ApiIdentifier = request.RepositoryUrl,
                Value = count,
                Unit = "operations",
                Timestamp = DateTime.UtcNow
            }
        };

        return Ok(response);
    }
}
