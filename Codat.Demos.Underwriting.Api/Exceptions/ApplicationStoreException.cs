namespace Codat.Demos.Underwriting.Api.Exceptions;

public class ApplicationStoreException : Exception
{
    public ApplicationStoreException(string message, Exception? innerException = default) : base(message, innerException)
    {
    }
}