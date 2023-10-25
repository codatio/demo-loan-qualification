namespace Codat.Demos.Underwriting.Api.Extensions;

public static class StringExtensions
{
    public static Guid ToGuid(this string input) => Guid.Parse(input);
}