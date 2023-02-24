using System.Text;
using Codat.Demos.Underwriting.Api.DataClients;
using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Extensions;
using Codat.Demos.Underwriting.Api.Models;
using Codat.Demos.Underwriting.Api.Orchestrators;
using Codat.Demos.Underwriting.Api.Services;
using Microsoft.Net.Http.Headers;

namespace Codat.Demos.Underwriting.Api;

public static class BindingModule
{
    private const string CodatUrl = "https://api.codat.io";
    private const string ContentType = "application/json";
    
    public static IServiceCollection Bind(this IServiceCollection services, IConfiguration configuration)
    {
        AddCodatHttpClient(services, configuration);
        
        services.Configure<UnderwritingParameters>(configuration.GetSection("AppSettings:UnderwritingParameters"));
        
        services.AddSingleton<IApplicationStore, ApplicationStore>();
        services.AddSingleton<ICodatDataClient, CodatDataClient>();
        services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
        services.AddSingleton<ILoanUnderwriter, LoanUnderwriter>();

        return services;
    }

    private static void AddCodatHttpClient(IServiceCollection services, IConfiguration configuration)
    {
        var apiKeyParam = "AppSettings:CodatApiKey";
        var apiKey = configuration.GetSection(apiKeyParam).Value;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ConfigurationMissingException(apiKeyParam);
        }

        var encodedApiKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey));
        
        services.AddHttpClient("Codat", httpClient =>
        {
            httpClient.BaseAddress = new Uri(CodatUrl);
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, ContentType);
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, $"Basic {encodedApiKey}");
        });
    }
}