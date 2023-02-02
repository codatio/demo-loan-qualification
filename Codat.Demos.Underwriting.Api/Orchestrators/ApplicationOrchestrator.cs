using Codat.Demos.Underwriting.Api.DataClients;
using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Extensions;
using Codat.Demos.Underwriting.Api.Models;
using Codat.Demos.Underwriting.Api.Services;

namespace Codat.Demos.Underwriting.Api.Orchestrators;

public interface IApplicationOrchestrator
{
    Task<ApplicationForm> CreateApplicationAsync();
    Task SubmitApplicationDetailsAsync(ApplicationForm form);
    ApplicationStatus GetApplicationStatus(Guid id);
    ApplicationForm GetApplication(Guid id);

    Task UpdateCodatDataConnectionAsync(CodatDataConnectionStatusAlert alert);
    Task UpdateDataTypeSyncStatusAsync(CodatDataSyncCompleteAlert alert);
    Task UpdateAccountCategorisationStatusAsync(CodatAccountCategorisationAlert alert);
}

public class ApplicationOrchestrator : IApplicationOrchestrator
{
    private readonly IApplicationStore _applicationStore;
    private readonly ICodatDataClient _codatDataClient;
    private readonly ILoanUnderwriter _underwriter;
    private readonly List<string> _accountingPlatformKeys = new();
    private static readonly ApplicationDataRequirements[] ApplicationRequirements = Enum.GetValues(typeof(ApplicationDataRequirements)).Cast<ApplicationDataRequirements>().ToArray();

    public ApplicationOrchestrator(IApplicationStore applicationStore, ICodatDataClient codatDataClient, ILoanUnderwriter underwriter)
    {
        _applicationStore = applicationStore;
        _codatDataClient = codatDataClient;
        _underwriter = underwriter;
    }

    public async Task<ApplicationForm> CreateApplicationAsync()
    {
        var applicationId = Guid.NewGuid();
        var company = await _codatDataClient.CreateCompanyAsync(applicationId.ToString());
        return _applicationStore.CreateApplication(applicationId, company.Id);
    }

    public async Task SubmitApplicationDetailsAsync(ApplicationForm form)
    {
        if (form.LoanAmount is not > 0m || form.LoanTerm is not > 11)
        {
            throw new ApplicationOrchestratorException("Loan amount and/or term is invalid. Amount have a positive, non-zero value. Term must be at least 12 months");
        }

        _applicationStore.SetApplicationDetails(form.Id, form.CompanyName, form.FullName, form.LoanPurpose, form.LoanAmount.Value, form.LoanTerm!.Value);
        var application = _applicationStore.GetApplication(form.Id);
        _applicationStore.AddFulfilledRequirementForCompany(application.CodatCompanyId, ApplicationDataRequirements.ApplicationDetails);
        _applicationStore.UpdateApplicationStatus(form.Id, ApplicationStatus.CollectingData);
        
        await TryUnderwriteLoanAsync(form.Id);

    }

    public ApplicationForm GetApplication(Guid id)
    {
        try
        {
            return _applicationStore.GetApplication(id);
        }
        catch (ApplicationStoreException e)
        {
            throw new ApplicationOrchestratorException(e.Message, e);
        }
    }

    public ApplicationStatus GetApplicationStatus(Guid id)
        => _applicationStore.GetApplicationStatus(id);

    public async Task UpdateCodatDataConnectionAsync(CodatDataConnectionStatusAlert alert)
    {
        var isAccountingPlatform = await IsAccountingPlatformAsync(alert.Data.PlatformKey);
        if (isAccountingPlatform)
        {
            _applicationStore.SetAccountingConnectionForCompany(alert.CompanyId, alert.Data.DataConnectionId);
            if (alert.Data.NewStatus.Equals("Linked", StringComparison.Ordinal))
            {
                var application = _applicationStore.GetApplicationByCompanyId(alert.CompanyId);
                _applicationStore.UpdateApplicationStatus(application.Id, ApplicationStatus.CollectingData);
            }
        }
    }
    
    public async Task UpdateDataTypeSyncStatusAsync(CodatDataSyncCompleteAlert alert)
    {
        var application = _applicationStore.GetApplicationByCompanyId(alert.CompanyId);

        if (application.AccountingConnection is null)
        {
            throw new ApplicationOrchestratorException(
                $"Cannot update data type sync status as no accounting data connection exists with id {alert.DataConnectionId}");
        }
        
        if (application.AccountingConnection.Id != alert.DataConnectionId)
        {
            return; 
        }

        var requirement = GetRequirementByDataType(alert.Data.DataType);
        if (requirement is null)
        {
            return;
        }
        _applicationStore.AddFulfilledRequirementForCompany(alert.CompanyId, requirement.Value);

        await TryUnderwriteLoanAsync(application.Id);
    }

    public async Task UpdateAccountCategorisationStatusAsync(CodatAccountCategorisationAlert alert)
    {
        //The webhook sent by codat is pretty useless as it provides little context on what has changed.
        //i.e. if all categories have been successfully categorised or there are N accounts requiring manual intervention.
        //Work around is to retrieve the metrics and check for that no UncategorisedAccount errors exist.

        var application = _applicationStore.GetApplicationByCompanyId(alert.CompanyId);

        //Easiest way to check if the accounts are classified is to call the financial metrics endpoint!
        var results = await GetFinancialMetrics(application);

        if (results.Metrics.SafeSelectMany(x => x.Errors).All(x => x.Type != "UncategorizedAccounts"))
        {
            _applicationStore.AddFulfilledRequirementForCompany(application.CodatCompanyId, ApplicationDataRequirements.AccountsClassified);
        }

        await TryUnderwriteLoanAsync(application.Id);
    }
    
    private static ApplicationDataRequirements? GetRequirementByDataType(string dataType)
        => dataType switch 
        {
            "chartOfAccounts" => ApplicationDataRequirements.ChartOfAccounts, 
            "balanceSheet" => ApplicationDataRequirements.BalanceSheet,
            "profitAndLoss" => ApplicationDataRequirements.ProfitAndLoss, 
            _ => null 
        };
    
    
    private async Task TryUnderwriteLoanAsync(Guid id)
    {
        UpdateApplicationStatusGivenRequirements(id);
        if (_applicationStore.GetApplicationStatus(id) == ApplicationStatus.DataCollectionComplete)
        {
            await UnderwriteLoanAsync(id);
        }
    }

    private void UpdateApplicationStatusGivenRequirements(Guid id)
    {
        var updatedApplication = _applicationStore.GetApplication(id);
        var requirementsMet = ApplicationRequirements.All(x => updatedApplication.Requirements.Any(y => x == y));
        var status = requirementsMet ? ApplicationStatus.DataCollectionComplete : ApplicationStatus.CollectingData;
        _applicationStore.UpdateApplicationStatus(id, status);
    }

    private async Task UnderwriteLoanAsync(Guid id)
    {
        _applicationStore.UpdateApplicationStatus(id, ApplicationStatus.Underwriting);
        var application = _applicationStore.GetApplication(id);
        var (profitAndLoss, balanceSheet) = await GetFinancialDataAsync(application);
        
        try
        {
            var outcome = _underwriter.Process(application.LoanAmount!.Value, application.LoanTerm!.Value, profitAndLoss, balanceSheet);
            _applicationStore.UpdateApplicationStatus(id, outcome);
        }
        catch (LoanUnderwriterException)
        {
            _applicationStore.UpdateApplicationStatus(id, ApplicationStatus.UnderwritingFailure);
        }
    }

    private async Task<(Report profitAndLoss, Report balanceSheet)> GetFinancialDataAsync(ApplicationForm application)
    {
        var profitAndLossTask = _codatDataClient.GetPreviousTwelveMonthsEnhancedProfitAndLossAsync(
            application.CodatCompanyId,
            application.AccountingConnection!.Id,
            application.DateCreated);
        
        var balanceSheetTask = _codatDataClient.GetPreviousTwelveMonthsEnhancedBalanceSheetAsync(
            application.CodatCompanyId,
            application.AccountingConnection!.Id,
            application.DateCreated);

        await Task.WhenAll(profitAndLossTask, balanceSheetTask);

        return (profitAndLossTask.Result, balanceSheetTask.Result);
    }

    private async Task<bool> IsAccountingPlatformAsync(string platformKey)
    {
        if (_accountingPlatformKeys.Count == 0)
        {
            var platforms = await _codatDataClient.GetAccountingPlatformsAsync();
            _accountingPlatformKeys.AddRange(platforms.Select(x => x.Key));
        }

        return _accountingPlatformKeys.Contains(platformKey);
    }

    private async Task<FinancialMetrics> GetFinancialMetrics(ApplicationForm application)
    {
        if (application.AccountingConnection is null)
        {
            throw new ApplicationOrchestratorException($"No accounting data connection registered for application id {application.Id}");
        }
        
        var results = await _codatDataClient.GetPreviousTwelveMonthsMetricsAsync(
            application.CodatCompanyId,
            application.AccountingConnection.Id,
            application.DateCreated);

        return results;
    }
}