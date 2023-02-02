using System.Configuration;

namespace Codat.Demos.Underwriting.Api.Exceptions;

[Serializable]
public class ConfigurationMissingException : Exception
{
    public ConfigurationMissingException(string paramName) : base($"Missing parameter '{paramName}' in app settings")
    {
    }
}