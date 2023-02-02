namespace Codat.Demos.Underwriting.Api.Models;

public record Report
{
    public Component[] ReportData { get; init; }
}

public record Component
{
    public string ItemDisplayName { get; init; }
    public List<Component> Components { get; init; }
    public List<Measure> Measures { get; init; }

    public Component Select(string itemDisplayName) 
        => Components.Single(x => x.ItemDisplayName.Equals(itemDisplayName));
}

public record Measure
{
    public decimal Value { get; init; }
}