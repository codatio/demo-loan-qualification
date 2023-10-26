using System.Net;
using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Extensions;
using Codat.Demos.Underwriting.Api.Models;
using Codat.Demos.Underwriting.Api.Services;
using CodatLending;
using CodatLending.Models.Shared;
using CodatPlatform;

namespace Codat.Demos.Underwriting.Api.Orchestrators;

public interface IApplicationOrchestrator
{
    Task<NewApplicationDetails> CreateApplicationAsync();
    Task SubmitApplicationDetailsAsync(Guid applicationId, ApplicationForm form);
    ApplicationStatus GetApplicationStatus(Guid id);
    Application GetApplication(Guid id);

    Task UpdateCodatDataConnectionAsync(CodatDataConnectionStatusAlert alert);
    Task UpdateDataTypeSyncStatusAsync(CodatDataSyncCompleteAlert alert);
    Task UpdateAccountCategorisationStatusAsync(CodatAccountCategorisationAlert alert);
}

public class ApplicationOrchestrator : IApplicationOrchestrator
{
    private readonly IApplicationStore _applicationStore;
    private readonly ICodatLendingSDK _codatLending;
    private readonly ICodatPlatformSDK _codatPlatform;
    private readonly ILoanUnderwriter _underwriter;
    private readonly List<string> _accountingPlatformKeys = new();
    private static readonly ApplicationDataRequirements[] ApplicationRequirements = Enum.GetValues(typeof(ApplicationDataRequirements)).Cast<ApplicationDataRequirements>().ToArray();

    public ApplicationOrchestrator(IApplicationStore applicationStore, ICodatLendingSDK codatLending, ICodatPlatformSDK codatPlatform, ILoanUnderwriter underwriter)
    {
        _applicationStore = applicationStore;
        _codatLending = codatLending;
        _codatPlatform = codatPlatform;
        _underwriter = underwriter;
    }

    public async Task<NewApplicationDetails> CreateApplicationAsync()
    {
        var applicationId = Guid.NewGuid();
        var companyResponse = await _codatLending.Companies.CreateAsync(new(){ Name = applicationId.ToString() });
        
        if (companyResponse.StatusCode != (int)HttpStatusCode.OK)//HttpStatus
        {
            if (companyResponse.ErrorMessage is null)
            {
                throw new ApplicationOrchestratorException("Failed to create company");
            }
            throw new ApplicationOrchestratorException(
                $"Failed to create company for the following reason {companyResponse.ErrorMessage.Error}");
        }
        return _applicationStore.CreateApplication(applicationId, companyResponse.Company!.Id.ToGuid());
    }

    public async Task SubmitApplicationDetailsAsync(Guid applicationId, ApplicationForm form)
    {
        if (form.LoanAmount is not > 0m || form.LoanTerm is not > 11)
        {
            throw new ApplicationOrchestratorException("Loan amount and/or term is invalid. Amount have a positive, non-zero value. Term must be at least 12 months");
        }

        _applicationStore.SetApplicationForm(applicationId, form);
        var application = _applicationStore.GetApplication(applicationId);
        _applicationStore.AddFulfilledRequirementForCompany(application.CodatCompanyId, ApplicationDataRequirements.ApplicationDetails);
        _applicationStore.UpdateApplicationStatus(applicationId, ApplicationStatus.CollectingData);
        
        await TryUnderwriteLoanAsync(applicationId);

    }

    public Application GetApplication(Guid id)
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
        
        if (application.AccountingConnection != alert.DataConnectionId)
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

    public Task UpdateAccountCategorisationStatusAsync(CodatAccountCategorisationAlert alert)
    {
        var application = _applicationStore.GetApplicationByCompanyId(alert.CompanyId);

        _applicationStore.AddFulfilledRequirementForCompany(application.CodatCompanyId, ApplicationDataRequirements.AccountsClassified);

        return TryUnderwriteLoanAsync(application.Id);
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
        var form = application.Form ?? throw new ApplicationOrchestratorException($"No form exists for application {id}.");
        
        try
        {
            var outcome = _underwriter.Process(form.LoanAmount, form.LoanTerm, profitAndLoss, balanceSheet);
            _applicationStore.UpdateApplicationStatus(id, outcome);
        }
        catch (LoanUnderwriterException)
        {
            _applicationStore.UpdateApplicationStatus(id, ApplicationStatus.UnderwritingFailure);
        }
    }

    private async Task<(FinancialStatement profitAndLoss, FinancialStatement balanceSheet)> GetFinancialDataAsync(Application application)
    {
        var reportDate = $"01-{application.DateCreated.AddMonths(-1):MM-yyyy}";
        var numberOfPeriods = 12;
        
        var profitAndLossTask = _codatLending.FinancialStatements.ProfitAndLoss.GetCategorizedAccountsAsync(new()
        {
            CompanyId = application.CodatCompanyId.ToString(),
            NumberOfPeriods = numberOfPeriods,
            ReportDate = reportDate
        });
        
        var balanceSheetTask = _codatLending.FinancialStatements.BalanceSheet.GetCategorizedAccountsAsync(new()
        {
            CompanyId = application.CodatCompanyId.ToString(),
            NumberOfPeriods = numberOfPeriods,
            ReportDate = reportDate
        });
        
        await Task.WhenAll(profitAndLossTask, balanceSheetTask);

        var profitAndLoss = MapToFinancialStatement(profitAndLossTask.Result.EnhancedFinancialReport, FinancialStatementType.ProfitAndLoss);
        var balanceSheet = MapToFinancialStatement(balanceSheetTask.Result.EnhancedFinancialReport, FinancialStatementType.BalanceSheet);
        
        return (profitAndLoss, balanceSheet);
    }

    private async Task<bool> IsAccountingPlatformAsync(string platformKey)
    {
        if (_accountingPlatformKeys.Count == 0)
        {
            var request = await _codatPlatform.Integrations.ListAsync(new()
            {
                Query = "sourceType=Accounting"
            });

            if (request.Integrations?.Results != null)
            {
                _accountingPlatformKeys.AddRange(request.Integrations.Results.Select(x => x.Key));
            }
        }

        return _accountingPlatformKeys.Contains(platformKey);
    }

    private static FinancialStatement MapToFinancialStatement(EnhancedFinancialReport report, FinancialStatementType statementType)
        => new()
        {
            Type = statementType,
            Lines = report.ReportItems.Select(x =>
                new FinancialStatementLine
                {
                    AccountCategorization = string.Join(".", x.AccountCategory.Levels.Select(y => y.LevelName)),
                    Balance = x.Balance ?? Decimal.Zero,
                    Date = Convert.ToDateTime(x.Date)
                }
            ).ToArray()
        };
}