namespace Codat.Demos.Underwriting.Api.Models;

public record Rule
{
    public Guid? Id { get; init; }
    public string Type { get; init; }
    public Notifiers Notifiers { get; init; }
}

public record Notifiers
{
    public string Webhook { get; init; }
}
