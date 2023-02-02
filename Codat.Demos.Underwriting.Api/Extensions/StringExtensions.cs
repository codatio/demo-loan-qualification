namespace Codat.Demos.Underwriting.Api.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrWhitespace(this string input) => string.IsNullOrWhiteSpace(input);
    public static bool IsNotNullOrWhitespace(this string input) => !string.IsNullOrWhiteSpace(input);
}