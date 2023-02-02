namespace Codat.Demos.Underwriting.Api.Models;

public record UnderwritingParameters
{
    public decimal MinGrossProfitMargin { get; init; }
    public decimal LoanCommissionPercentage { get; init; }
    public decimal RevenueThreshold { get; init; }
    public decimal MaxGearingRatio { get; init; }
}