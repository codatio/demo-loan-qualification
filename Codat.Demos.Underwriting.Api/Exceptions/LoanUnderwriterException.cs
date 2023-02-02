namespace Codat.Demos.Underwriting.Api.Exceptions;

public class LoanUnderwriterException : Exception
{
    public LoanUnderwriterException(string message, Exception? innerException = default) : base(message, innerException)
    {
    }
}