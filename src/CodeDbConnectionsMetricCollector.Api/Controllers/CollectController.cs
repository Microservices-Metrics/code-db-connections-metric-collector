using Microsoft.AspNetCore.Mvc;
using CodeDbConnectionsMetricCollector.Api.Models;
using CodeDbConnectionsMetricCollector.Api.Services;
using System.Net;

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

        _logger.LogInformation("Analyzing repository: {Url} (dockerComposeAnalysis={Flag})",
            request.RepositoryUrl, request.DockerComposeAnalysis);

        int count;
        try
        {
            count = request.DockerComposeAnalysis
                ? await _analyzerService.CountDockerComposeDatabasesAsync(request.RepositoryUrl, cancellationToken)
                : await _analyzerService.CountDatabaseConnectionsAsync(request.RepositoryUrl, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Repository archive not found for URL: {Url}", request.RepositoryUrl);
            return NotFound("Repository archive not found. Check if the repository exists and is publicly accessible.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upstream request failed while analyzing repository: {Url}", request.RepositoryUrl);
            return StatusCode(StatusCodes.Status502BadGateway,
                "Failed to download repository from the remote host. Try again later.");
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "Invalid repository archive for URL: {Url}", request.RepositoryUrl);
            return StatusCode(StatusCodes.Status502BadGateway,
                "The repository archive could not be read. Ensure the repository URL is valid and accessible.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while processing repository: {Url}", request.RepositoryUrl);
            return StatusCode(StatusCodes.Status502BadGateway,
                "An I/O error occurred while processing repository contents. Try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while analyzing repository: {Url}", request.RepositoryUrl);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while analyzing the repository.");
        }

        var response = new CollectResponse
        {
            Metric = new MetricInfo
            {
                Name = "Number of Database Connections",
                CollectorStrategy = request.DockerComposeAnalysis ? "docker compose" : "source code"
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
