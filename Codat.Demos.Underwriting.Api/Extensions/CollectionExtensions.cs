namespace Codat.Demos.Underwriting.Api.Extensions;

public static class CollectionExtensions
{
    public static IEnumerable<TResult> SafeSelectMany<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
    {
        foreach (var subEnumerable in source.SafeSelect(selector))
        {
            if (subEnumerable is not null)
            {
                foreach (var subItem in subEnumerable)
                {
                    yield return subItem;
                }
            }
        }
    }
    
    private static IEnumerable<TResult> SafeSelect<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
        => source?.Select(selector) ?? Array.Empty<TResult>();
}