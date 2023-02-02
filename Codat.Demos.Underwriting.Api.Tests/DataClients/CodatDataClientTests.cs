using System.Net;
using Codat.Demos.Underwriting.Api.DataClients;
using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;
using FluentAssertions;
using Moq;
using SoloX.CodeQuality.Test.Helpers.Http;
using Xunit;

namespace Codat.Demos.Underwriting.Api.Tests.DataClients;

public class CodatDataClientTests
{
    private const string CodatClientName = "Codat";
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new(MockBehavior.Strict);
    private readonly ICodatDataClient _client;

    private const string CodatCompanyName = "Test Company";
    private readonly Company _company = new(){ Id = Guid.NewGuid(), Name = CodatCompanyName };
    private readonly DataConnection[] _dataConnections = { new(){ Id = Guid.NewGuid(), } };
    private readonly Platform[] _platforms = { new() { Key = "gbol", Name = "Zero"} };
    
    public static readonly IEnumerable<object[]> UnsuccessfulStatusCodes = Enum.GetValues(typeof(HttpStatusCode))
        .Cast<HttpStatusCode>()
        .Where(x => !(HttpStatusCode.OK <= x && x < HttpStatusCode.MultipleChoices))
        .Select(x => new object[]{ x });
    
    public CodatDataClientTests()
    {
        _client = new CodatDataClient(_httpClientFactory.Object);
    }

    [Fact]
    public async Task CreateCompanyAsync_returns_companyId_when_company_created_successfully()
    {
        SetupCreateCompaniesEndpoint(HttpStatusCode.OK);
        var companyId = await _client.CreateCompanyAsync(CodatCompanyName);
        companyId.Should().BeEquivalentTo(_company);
    }
    
    [Theory]
    [MemberData(nameof(UnsuccessfulStatusCodes))]
    public async Task CreateCompanyAsync_throws_exception_when_response_code_is_not_success(HttpStatusCode statusCode)
    {
        SetupCreateCompaniesEndpoint(statusCode);
        await TestUnsuccessfulErrorCodes(_client.CreateCompanyAsync(CodatCompanyName), statusCode);
    }

    private async Task TestUnsuccessfulErrorCodes<T>(Task<T> actionTask, HttpStatusCode statusCode)
    {
        var response = async () => await actionTask;
        await response.Should().ThrowAsync<CodatDataClientException>().WithMessage($"Failed with status code {(int)statusCode} ({statusCode})");
    }


    [Fact] 
    public async Task CreateCompanyAsync_throws_exception_when_company_returned_is_null()
    {
        var builder = GetMockHttpClientBuilder()
            .WithRequest("/companies", HttpMethod.Post)
            .RespondingJsonContent((Company)null!);
        
        SetupHttpClientFactory(builder);
        var response = async () => await _client.CreateCompanyAsync(CodatCompanyName);
        await response.Should().ThrowAsync<CodatDataClientException>().WithMessage("Json object is null");
    }
    
    [Fact]
    public async Task GetAccountingPlatformsAsync_returns_companyId_when_company_created_successfully()
    {
        SetupGetAccountingPlatformsEndpoint(HttpStatusCode.OK);
        var dataConnections = await _client.GetAccountingPlatformsAsync();
        dataConnections.Should().HaveCount(_platforms.Length);
    }
    
    [Theory]
    [MemberData(nameof(UnsuccessfulStatusCodes))]
    public async Task GetAccountingPlatformsAsync_throws_exception_when_response_code_is_not_success(HttpStatusCode statusCode)
    {
        SetupGetAccountingPlatformsEndpoint(statusCode);
        await TestUnsuccessfulErrorCodes(_client.GetAccountingPlatformsAsync(), statusCode);
    }
    
    [Fact]
    public async Task GetAccountingPlatformsAsync_throws_exception_when_paginated_data_connections_returned_is_null()
    {
        var builder = GetMockHttpClientBuilder()
            .WithRequest("/integrations", HttpMethod.Get)
            .RespondingJsonContent((CodatPaginatedResponse<Platform>)null!);
        
        SetupHttpClientFactory(builder); 
        
        var response = async () => await _client.GetAccountingPlatformsAsync();
        await response.Should().ThrowAsync<CodatDataClientException>().WithMessage("Json object is null");
    }
    
    [Fact]
    public async Task GetPreviousTwelveMonthsMetricsAsync_returns_companyId_when_company_created_successfully()
    {
        var dataConnectionId = Guid.NewGuid();
        var expectedFinancialMetrics = new FinancialMetrics { Metrics = new[] { new FinancialMetric() { Key = "test " } } };
        SetupGetAssessEndpoint("financialMetrics", _company.Id, dataConnectionId, expectedFinancialMetrics, HttpStatusCode.OK);
        var financialMetrics = await _client.GetPreviousTwelveMonthsMetricsAsync(_company.Id, dataConnectionId, DateTime.UtcNow);
        financialMetrics.Should().BeEquivalentTo(expectedFinancialMetrics);
    }
    
    [Theory]
    [MemberData(nameof(UnsuccessfulStatusCodes))]
    public async Task GetPreviousTwelveMonthsMetricsAsync_throws_exception_when_response_code_is_not_success(HttpStatusCode statusCode)
    {
        var dataConnectionId = Guid.NewGuid();
        SetupGetAssessEndpoint("financialMetrics", _company.Id, dataConnectionId, new FinancialMetrics{ Metrics = new[] { new FinancialMetric(){ Key = "test "} } }, statusCode);
        await TestUnsuccessfulErrorCodes(_client.GetPreviousTwelveMonthsMetricsAsync(_company.Id, dataConnectionId, DateTime.UtcNow), statusCode);
    }
    
    [Fact]
    public async Task GetPreviousTwelveMonthsEnhancedProfitAndLossAsync_returns_companyId_when_company_created_successfully()
    {
        var dataConnectionId = Guid.NewGuid();
        var expectedFinancialMetrics = new Report { ReportData = new[] { new Component() { ItemDisplayName = "test " } } };
        SetupGetAssessEndpoint("enhancedProfitAndLoss", _company.Id, dataConnectionId, expectedFinancialMetrics, HttpStatusCode.OK);
        var financialMetrics = await _client.GetPreviousTwelveMonthsEnhancedProfitAndLossAsync(_company.Id, dataConnectionId, DateTime.UtcNow);
        financialMetrics.Should().BeEquivalentTo(expectedFinancialMetrics);
    }
    
    [Theory]
    [MemberData(nameof(UnsuccessfulStatusCodes))]
    public async Task GetPreviousTwelveMonthsEnhancedProfitAndLossAsync_throws_exception_when_response_code_is_not_success(HttpStatusCode statusCode)
    {
        var dataConnectionId = Guid.NewGuid();
        SetupGetAssessEndpoint("enhancedProfitAndLoss", _company.Id, dataConnectionId, new Report { ReportData = new[] { new Component() { ItemDisplayName = "test " } } }, statusCode);
        await TestUnsuccessfulErrorCodes(_client.GetPreviousTwelveMonthsEnhancedProfitAndLossAsync(_company.Id, dataConnectionId, DateTime.UtcNow), statusCode);
    }
    
    [Fact]
    public async Task GetPreviousTwelveMonthsEnhancedBalanceSheetAsync_returns_companyId_when_company_created_successfully()
    {
        var dataConnectionId = Guid.NewGuid();
        var expectedFinancialMetrics = new Report { ReportData = new[] { new Component() { ItemDisplayName = "test " } } };
        SetupGetAssessEndpoint("enhancedBalanceSheet", _company.Id, dataConnectionId, expectedFinancialMetrics, HttpStatusCode.OK);
        var financialMetrics = await _client.GetPreviousTwelveMonthsEnhancedBalanceSheetAsync(_company.Id, dataConnectionId, DateTime.UtcNow);
        financialMetrics.Should().BeEquivalentTo(expectedFinancialMetrics);
    }
    
    [Theory]
    [MemberData(nameof(UnsuccessfulStatusCodes))]
    public async Task GetPreviousTwelveMonthsEnhancedBalanceSheetAsync_throws_exception_when_response_code_is_not_success(HttpStatusCode statusCode)
    {
        var dataConnectionId = Guid.NewGuid();
        SetupGetAssessEndpoint("enhancedBalanceSheet", _company.Id, dataConnectionId, new Report { ReportData = new[] { new Component() { ItemDisplayName = "test " } } }, statusCode);
        await TestUnsuccessfulErrorCodes(_client.GetPreviousTwelveMonthsEnhancedBalanceSheetAsync(_company.Id, dataConnectionId, DateTime.UtcNow), statusCode);
    }

    private void SetupHttpClientFactory(IHttpClientRequestMockBuilder builder)
        => _httpClientFactory
            .Setup(x => x.CreateClient(It.Is<string>(y => y.Equals(CodatClientName, StringComparison.Ordinal))))
            .Returns(builder.Build());

    private static IHttpClientRequestMockBuilder GetMockHttpClientBuilder()
        => new HttpClientMockBuilder().WithBaseAddress(new Uri("https://expected-website.com"));
    
    private void SetupCreateCompaniesEndpoint(HttpStatusCode statusCode)
    {
        var builder = GetMockHttpClientBuilder()
            .WithRequest("/companies", HttpMethod.Post)
            .RespondingJsonContent(_company, statusCode);
        
        SetupHttpClientFactory(builder);
    }
    
    private void SetupGetAssessEndpoint<T>(string endpoint, Guid companyId, Guid dataConnectionId, T body, HttpStatusCode statusCode)
    {
        var builder = GetMockHttpClientBuilder()
            .WithRequest($"/data/companies/{companyId}/connections/{dataConnectionId}/assess/{endpoint}", HttpMethod.Get)
            .RespondingJsonContent(body, statusCode);
        SetupHttpClientFactory(builder);
    }

    private void SetupGetAccountingPlatformsEndpoint(HttpStatusCode statusCode)
    {
        var builder = GetMockHttpClientBuilder()
            .WithRequest("/integrations", HttpMethod.Get)
            .RespondingJsonContent(new CodatPaginatedResponse<Platform>{ Results = _platforms }, statusCode);
        
        SetupHttpClientFactory(builder);
    }
}