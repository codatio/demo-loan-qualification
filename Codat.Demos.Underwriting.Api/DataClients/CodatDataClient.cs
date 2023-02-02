using System.Net;
using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;

namespace Codat.Demos.Underwriting.Api.DataClients;

public interface ICodatDataClient
{
    
    Task<Company> CreateCompanyAsync(string companyName);
    Task<Platform[]> GetAccountingPlatformsAsync();
    Task<FinancialMetrics> GetPreviousTwelveMonthsMetricsAsync(Guid companyId, Guid dataConnectionId, DateTime reportDate);
    Task<Report> GetPreviousTwelveMonthsEnhancedProfitAndLossAsync(Guid companyId, Guid dataConnectionId, DateTime reportDate);
    Task<Report> GetPreviousTwelveMonthsEnhancedBalanceSheetAsync(Guid companyId, Guid dataConnectionId, DateTime reportDate);

    #region Specific set up endpoints for posting webhooks.
    Task<Rule> CreateWebhookRuleAsync(string ruleType, string webhookUrl);
    Task TryDeleteRuleAsync(Guid id);
    #endregion
    
}

public class CodatDataClient : ICodatDataClient
{
    private readonly IHttpClientFactory _clientFactory;

    public CodatDataClient(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<Company> CreateCompanyAsync(string companyName)
    {
        var newCompanyRequestObject = new Company
        {
            Name = companyName
        };

        var response = await Client.PostAsJsonAsync("/companies", newCompanyRequestObject);

        if (!response.IsSuccessStatusCode)
        {
            ThrowDataClientExceptionForHttpResponse(response);
        }

        var company = await response.Content.ReadFromJsonAsync<Company>();

        AssertObjectIsNotNull(company);

        return company!;
    }
    
    public Task<Platform[]> GetAccountingPlatformsAsync()
        => ProcessPaginatedResponse<Platform>(x => x.GetAsync("/integrations?page=1&pageSize=2000&query=sourceType%3DAccounting"));

    public Task<FinancialMetrics> GetPreviousTwelveMonthsMetricsAsync(Guid companyId, Guid dataConnectionId, DateTime reportDate)
        => ExecuteGetRequestAsync<FinancialMetrics>(
            $"{GetAssessUriString(companyId, dataConnectionId)}/financialMetrics?{GetAssessQueryString(reportDate)}");

    public Task<Report> GetPreviousTwelveMonthsEnhancedProfitAndLossAsync(Guid companyId, Guid dataConnectionId, DateTime reportDate)
        => ExecuteGetRequestAsync<Report>(
            $"{GetAssessUriString(companyId, dataConnectionId)}/enhancedProfitAndLoss?{GetAssessQueryString(reportDate)}&includeDisplayNames=true");

    public Task<Report> GetPreviousTwelveMonthsEnhancedBalanceSheetAsync(Guid companyId, Guid dataConnectionId, DateTime reportDate)
        => ExecuteGetRequestAsync<Report>(
            $"{GetAssessUriString(companyId, dataConnectionId)}/enhancedBalanceSheet?{GetAssessQueryString(reportDate)}&includeDisplayNames=true");
    
    private async Task<T> ExecuteGetRequestAsync<T>(string endpoint)
    {
        var response = await Client.GetAsync(endpoint);

        if (!response.IsSuccessStatusCode)
        {
            ThrowDataClientExceptionForHttpResponse(response);
        }

        var body = await response.Content.ReadFromJsonAsync<T>();
        
        AssertObjectIsNotNull(body);

        return body;
    }
    
    private HttpClient Client => _clientFactory.CreateClient("Codat");

    private static void ThrowDataClientExceptionForHttpResponse(HttpResponseMessage response)
    {
        throw new CodatDataClientException($"Failed with status code {(int)response.StatusCode} ({response.StatusCode})");
    }
    
    private static string GetAssessUriString(Guid companyId, Guid dataConnectionId)
        => $"/data/companies/{companyId}/connections/{dataConnectionId}/assess";
    
    private static string GetAssessQueryString(DateTime dateTo)
        => $"reportDate=01-{dateTo.AddMonths(-1):MM-yyyy}&periodLength=12&numberOfPeriods=1";

    private async Task<T[]> ProcessPaginatedResponse<T>(Func<HttpClient, Task<HttpResponseMessage>> request)
    {
        var response = await request.Invoke(Client);
        if (!response.IsSuccessStatusCode)
        {
            ThrowDataClientExceptionForHttpResponse(response);
        }
        
        var paginatedResponse = await response.Content.ReadFromJsonAsync<CodatPaginatedResponse<T>>();
        
        AssertObjectIsNotNull(paginatedResponse);

        return paginatedResponse!.Results;
    }
    
    private static void AssertObjectIsNotNull<T>(T input)
    {
        if (input is null)
        {
            throw new CodatDataClientException("Json object is null");
        }
    }
    
    #region Specific set up endpoints for posting webhooks.
    
    public async Task<Rule> CreateWebhookRuleAsync(string ruleType, string webhookUrl)
    {
        var ruleRequest = new Rule
        {
            Type = ruleType,
            Notifiers = new()
            {
                Webhook = webhookUrl
            }
        };
        
        var response = await Client.PostAsJsonAsync("/rules", ruleRequest);

        if (!response.IsSuccessStatusCode)
        {
            ThrowDataClientExceptionForHttpResponse(response);
        }
        
        var ruleCreated = await response.Content.ReadFromJsonAsync<Rule>();
        return ruleCreated!;
    }

    public async Task TryDeleteRuleAsync(Guid id)
    {
        var response = await Client.DeleteAsync($"rules/{id}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }
        
        if (!response.IsSuccessStatusCode)
        {
            ThrowDataClientExceptionForHttpResponse(response);
        }
    }
    
    #endregion
}