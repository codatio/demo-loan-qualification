using System.Text.Json;
using Codat.Demos.Underwriting.Api.Exceptions;

namespace Codat.Demos.Underwriting.Api.Helpers;

public static class StreamHelpers
{
    public static async Task<Stream> ToJsonStreamAsync<TObject>(this TObject objectToSerialise)
    {
        var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, objectToSerialise).ConfigureAwait(false);
        stream.Position = 0;
        return stream;
    }

    public static async Task<StreamContent> ToJsonStreamContentAsync<TObject>(this TObject objectToStream)
    {
        var stream = await ToJsonStreamAsync(objectToStream).ConfigureAwait(false);
        return new StreamContent(stream);
    }

    public static async Task<TObject?> ToObjectAsync<TObject>(this Stream stream)
    {
        if (!stream.CanRead)
        {
            throw new StreamHelperException("Cannot read stream");
        }

        stream.Position = 0;

        return await JsonSerializer.DeserializeAsync<TObject>(stream).ConfigureAwait(false);
    }
}