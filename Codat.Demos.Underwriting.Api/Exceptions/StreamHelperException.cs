namespace Codat.Demos.Underwriting.Api.Exceptions;

public class StreamHelperException : Exception
{
    public StreamHelperException(string message, Exception? innerException = default) : base(message, innerException)
    {
    }
}