namespace Codat.Demos.Underwriting.Api.Models;

public record CodatPaginatedResponse<TDataType>
{
    public TDataType[] Results { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalResults { get; init; }
}