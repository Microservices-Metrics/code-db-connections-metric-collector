namespace CodeDbConnectionsMetricCollector.Api.Models;

public class CollectRequest
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public bool DockerComposeAnalysis { get; set; } = false;
}
