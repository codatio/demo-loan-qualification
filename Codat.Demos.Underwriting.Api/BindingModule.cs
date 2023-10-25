using System.Text;
using Codat.Demos.Underwriting.Api.DataClients;
using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;
using Codat.Demos.Underwriting.Api.Orchestrators;
using Codat.Demos.Underwriting.Api.Services;
using CodatLending;
using CodatLending.Models.Shared;
using CodatPlatform;
using CodatPlatformSecurity = CodatPlatform.Models.Shared.Security;

namespace Codat.Demos.Underwriting.Api;

public static class BindingModule
{
    private const string  ApiKeyParam = "AppSettings:CodatApiKey";
    
    public static IServiceCollection Bind(this IServiceCollection services, IConfiguration configuration)
    {
        AddCodatLibraries(services, configuration);
        
        services.Configure<UnderwritingParameters>(configuration.GetSection("AppSettings:UnderwritingParameters"));
        
        services.AddSingleton<IApplicationStore, ApplicationStore>();
        services.AddSingleton<ICodatDataClient, CodatDataClient>();
        services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
        services.AddSingleton<ILoanUnderwriter, LoanUnderwriter>();

        return services;
    }

    private static void AddCodatLibraries(IServiceCollection services, IConfiguration configuration)
    {
        var apiKey = configuration.GetSection(ApiKeyParam).Value;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ConfigurationMissingException(ApiKeyParam);
        }

        var encodedApiKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey));
        var authHeaderValue = $"Basic {encodedApiKey}";
        
        services.AddSingleton<ICodatLendingSDK, CodatLendingSDK>(_ => new CodatLendingSDK(new Security{ AuthHeader = authHeaderValue }));
        services.AddSingleton<ICodatPlatformSDK, CodatPlatformSDK>(_ => new CodatPlatformSDK(new CodatPlatformSecurity{ AuthHeader = authHeaderValue }));
    }
}