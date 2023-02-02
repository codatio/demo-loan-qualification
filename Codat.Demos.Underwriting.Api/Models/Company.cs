namespace Codat.Demos.Underwriting.Api.Models;

public record Company
{
    public Guid Id { get; init; }
    public string Name { get; init; }
}