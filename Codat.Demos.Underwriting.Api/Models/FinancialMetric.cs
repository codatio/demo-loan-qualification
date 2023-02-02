namespace Codat.Demos.Underwriting.Api.Models;

public record FinancialMetrics
{
    public FinancialMetric[] Metrics { get; init; }
}

public record FinancialMetric
{
    public string Key { get; init; }
    public MetricPeriod[] Periods { get; init; }
    public MetricError[] Errors { get; init; }
}

public record MetricPeriod
{
    public decimal Value { get; init; }
}

public record MetricError
{
    public string Type { get; init; }
}