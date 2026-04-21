namespace CodeDbConnectionsMetricCollector.Api.Models;

public class MetricInfo
{
    public string Name { get; set; } = "Number of Database Connections";
    public string CollectorStrategy { get; set; } = "source code";
}

public class MeasurementInfo
{
    public string ApiIdentifier { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Unit { get; set; } = "operations";
    public DateTime Timestamp { get; set; }
}

public class CollectResponse
{
    public MetricInfo Metric { get; set; } = new();
    public MeasurementInfo Measurement { get; set; } = new();
}
