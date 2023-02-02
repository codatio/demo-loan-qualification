namespace Codat.Demos.Underwriting.Api.Exceptions;

public class ApplicationStatusStoreException : Exception
{
    public ApplicationStatusStoreException(string message, Exception? innerException = default) : base(message, innerException)
    {
    }
}