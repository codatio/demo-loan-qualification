using Codat.Demos.Underwriting.Api.DataClients;
using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;
using Codat.Demos.Underwriting.Api.Orchestrators;
using Codat.Demos.Underwriting.Api.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Codat.Demos.Underwriting.Api.Tests.Orchestrators;

public class ApplicationOrchestratorTests
{
    private readonly Mock<IApplicationStore> _applicationStore = new(MockBehavior.Strict);
    private readonly Mock<ICodatDataClient> _codatDataClient = new(MockBehavior.Strict);
    private readonly Mock<ILoanUnderwriter> _underwriter = new(MockBehavior.Strict);
    private readonly ApplicationOrchestrator _orchestrator;

    public static IEnumerable<object[]> InvalidLoanAmountsAndTerms()
    {
        yield return new object[] { 0M, 12 };
        yield return new object[] { -0.01M, 12 };
        yield return new object[] { 1000M, 11 };
    }
    
    public ApplicationOrchestratorTests()
    {
        _underwriter.Setup(x => x.Process(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<Report>(), It.IsAny<Report>()));

        _orchestrator = new ApplicationOrchestrator(_applicationStore.Object, _codatDataClient.Object, _underwriter.Object);
    }
    
    [Fact]
    public async Task CreateApplicationAsync_sets_application_id_and_codat_company_id_in_application_form()
    {
        var codatCompanyId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        
        _codatDataClient.Setup(x => x.CreateCompanyAsync(It.IsAny<string>()))
            .ReturnsAsync(new Company{ Id = codatCompanyId })
            .Verifiable();

        _applicationStore.Setup(x => x.CreateApplication(It.IsAny<Guid>(), It.Is<Guid>(y => y == codatCompanyId)))
            .Returns(new Application { Id = applicationId, CodatCompanyId = codatCompanyId })
            .Verifiable();

        var application = await _orchestrator.CreateApplicationAsync();
        application.Id.Should().Be(applicationId);
        application.CodatCompanyId.Should().Be(codatCompanyId);

        VerifyCodatClient();
        VerifyApplicationStore();
    }

    [Fact]
    public async Task SubmitApplicationDetails_passes_expected_fields_to_application_store()
    {
        var applicationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var form = new ApplicationForm
        {
            CompanyName = "Test Company", 
            FullName = "John Smith", 
            LoanAmount = 10000,
            LoanTerm = 36,
            LoanPurpose = "Growth marketing"
        };

        var application = new Application()
        {
            Id = applicationId,
            CodatCompanyId = companyId,
            Status = ApplicationStatus.Started,
            Form = form
        };
        
        _applicationStore.Setup(x => x.SetApplicationForm(
                It.Is<Guid>(y => y == applicationId),
                It.Is<ApplicationForm>(y => y == form)))
            .Verifiable();

        _applicationStore.Setup(x =>
            x.UpdateApplicationStatus(It.Is<Guid>(y => y == applicationId), It.Is<ApplicationStatus>(y => y == ApplicationStatus.CollectingData)))
            .Verifiable();

        _applicationStore.Setup(x => x.GetApplication(It.Is<Guid>(y => y == applicationId)))
            .Returns(application)
            .Verifiable();

        _applicationStore.Setup(x => x.GetApplicationStatus(It.Is<Guid>(y => y == applicationId)))
            .Returns(ApplicationStatus.CollectingData)
            .Verifiable();

        _applicationStore.Setup(x => x.AddFulfilledRequirementForCompany(
                It.Is<Guid>(y => y == companyId),
                It.Is<ApplicationDataRequirements>(y => y == ApplicationDataRequirements.ApplicationDetails)))
            .Verifiable();

        await _orchestrator.SubmitApplicationDetailsAsync(applicationId, form);

        VerifyApplicationStore();
        VerifyCodatClient();
        VerifyLoanUnderwriterIsNotCalled();
    }
    
    [Theory]
    [MemberData(nameof(InvalidLoanAmountsAndTerms))]
    public void SubmitApplicationDetailsAsync_throws_ApplicationOrchestratorException_when_loan_amount_or_term_is_invalid(decimal loanAmount, int loanTerm)
    {
        var applicationId = Guid.NewGuid();
        var form = new ApplicationForm
        {
            CompanyName = "Test Company",
            FullName = "John Smith", 
            LoanAmount = loanAmount, 
            LoanTerm = loanTerm
        };
        
        var action = () => _orchestrator.SubmitApplicationDetailsAsync(applicationId, form);
        action.Should()
            .ThrowAsync<ApplicationOrchestratorException>()
            .WithMessage("Loan amount and/or term is invalid. Amount have a positive, non-zero value. Term must be at least 12 months");
        
        VerifyApplicationStore();
        VerifyCodatClient();
        VerifyLoanUnderwriterIsNotCalled();
    }

    [Fact]
    public async Task UpdateCodatDataConnectionAsync_sets_accounting_connection_for_company()
    {
        var expectedPlatformKey = "mqjo";
        var codatCompanyId = Guid.NewGuid();
        
        var alert = CreateDataConnectionStatusAlert(codatCompanyId, "PendingAuth", expectedPlatformKey);
        
        _codatDataClient.Setup(x => x.GetAccountingPlatformsAsync())
            .ReturnsAsync(new Platform[] { new() { Key = expectedPlatformKey }, new(){ Key = "gbol"} })
            .Verifiable();
        
        _applicationStore.Setup(x => x.SetAccountingConnectionForCompany(
            It.Is<Guid>(y => y == alert.CompanyId),
            It.Is<Guid>(y => y == alert.Data.DataConnectionId)))
            .Verifiable();
        
        await _orchestrator.UpdateCodatDataConnectionAsync(alert);
        await _orchestrator.UpdateCodatDataConnectionAsync(alert);

        VerifyCodatClient();
        VerifyApplicationStore();
        _codatDataClient.Verify(x => x.GetAccountingPlatformsAsync(), Times.Once);
    }
    
    [Fact]
    public async Task UpdateCodatDataConnectionAsync_updates_application_status_when_data_connection_status_is_Linked()
    {
        var expectedPlatformKey = "mqjo";
        var codatCompanyId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var alert = CreateDataConnectionStatusAlert(codatCompanyId, "Linked", expectedPlatformKey);
        
        _codatDataClient.Setup(x => x.GetAccountingPlatformsAsync())
            .ReturnsAsync(new Platform[] { new() { Key = expectedPlatformKey }, new(){ Key = "gbol"} })
            .Verifiable();
        
        _applicationStore.Setup(x => x.SetAccountingConnectionForCompany(
                It.Is<Guid>(y => y == alert.CompanyId),
                It.Is<Guid>(y => y == alert.Data.DataConnectionId)))
            .Verifiable();

        _applicationStore.Setup(x => x.GetApplicationByCompanyId(It.Is<Guid>(y => y == codatCompanyId)))
            .Returns(new Application { Id = applicationId, CodatCompanyId = codatCompanyId })
            .Verifiable();
        
        _applicationStore.Setup(x => x.UpdateApplicationStatus(
                It.Is<Guid>(y => y == applicationId), 
                It.Is<ApplicationStatus>(y => y == ApplicationStatus.CollectingData)))
            .Verifiable();

        await _orchestrator.UpdateCodatDataConnectionAsync(alert);

        VerifyCodatClient();
        VerifyApplicationStore();
    }
    
    public static IEnumerable<object[]> ValidDataTypesAndAssociatedRequirements()
    {
        yield return new object[] { "chartOfAccounts", ApplicationDataRequirements.ChartOfAccounts };
        yield return new object[] { "balanceSheet", ApplicationDataRequirements.BalanceSheet };
        yield return new object[] { "profitAndLoss", ApplicationDataRequirements.ProfitAndLoss };
    }
    
    [Theory]
    [MemberData(nameof(ValidDataTypesAndAssociatedRequirements))]
    public async Task UpdateDataTypeSyncStatusAsync_updates_requirement_for_required_data_type(string dataType, ApplicationDataRequirements expectedRequirement)
    {
        var codatCompanyId = Guid.NewGuid();
        var dataConnectionId = Guid.NewGuid();
        
        var application = new Application
        {
            Id = Guid.NewGuid(),
            CodatCompanyId = codatCompanyId,
            AccountingConnection = dataConnectionId
        };

        _applicationStore.Setup(x => x.GetApplicationByCompanyId(It.Is<Guid>(y => y == codatCompanyId)))
            .Returns(application)
            .Verifiable();

        _applicationStore
            .Setup(x =>
                x.AddFulfilledRequirementForCompany(It.Is<Guid>(y => y == codatCompanyId), It.Is<ApplicationDataRequirements>(y => y == expectedRequirement)))
            .Verifiable();
        
        _applicationStore.Setup(x => x.GetApplication(It.IsAny<Guid>()))
            .Returns(application);

        _applicationStore.Setup(x => x.UpdateApplicationStatus(It.IsAny<Guid>(), It.IsAny<ApplicationStatus>()));

        _applicationStore.Setup(x => x.GetApplicationStatus(It.IsAny<Guid>()))
            .Returns(ApplicationStatus.CollectingData);

        var alert = CreateDataSyncCompleteAlert(codatCompanyId, dataConnectionId, dataType);

        await _orchestrator.UpdateDataTypeSyncStatusAsync(alert);

        _applicationStore.Verify(x => 
            x.AddFulfilledRequirementForCompany(It.Is<Guid>(y => y == codatCompanyId), It.Is<ApplicationDataRequirements>(y => y == expectedRequirement)),
            Times.AtLeastOnce);

        VerifyCodatClient();
    }

    [Fact]
    public async Task UpdateDataTypeSyncStatusAsync_should_throw_ApplicationOrchestratorException_when_not_accounting_connection_has_been_set()
    {
        var codatCompanyId = Guid.NewGuid();

        _applicationStore.Setup(x => x.GetApplicationByCompanyId(It.Is<Guid>(y => y == codatCompanyId)))
            .Returns(new Application
            {
                Id = Guid.NewGuid(),
                CodatCompanyId = codatCompanyId,
                AccountingConnection = null
            })
            .Verifiable();
        
        var alert = CreateDataSyncCompleteAlert(codatCompanyId);

        var action = () => _orchestrator.UpdateDataTypeSyncStatusAsync(alert);
        await action.Should().ThrowAsync<ApplicationOrchestratorException>()
            .WithMessage($"Cannot update data type sync status as no accounting data connection exists with id {alert.DataConnectionId}");
        
        VerifyApplicationStore();
        VerifyCodatClient();
    }
    
    [Fact]
    public async Task UpdateDataTypeSyncStatusAsync_ignores_data_connections_that_do_not_match_account_data_connection()
    {
        var codatCompanyId = Guid.NewGuid();

        _applicationStore.Setup(x => x.GetApplicationByCompanyId(It.Is<Guid>(y => y == codatCompanyId)))
            .Returns(new Application
            {
                Id = Guid.NewGuid(),
                CodatCompanyId = codatCompanyId,
                AccountingConnection = Guid.NewGuid()
            })
            .Verifiable();

        var alert = CreateDataSyncCompleteAlert(codatCompanyId);
        
        _applicationStore.Setup(x => 
            x.AddFulfilledRequirementForCompany(It.Is<Guid>(y => y == codatCompanyId), It.IsAny<ApplicationDataRequirements>()));

        await _orchestrator.UpdateDataTypeSyncStatusAsync(alert);

        _applicationStore.Verify(x => 
            x.AddFulfilledRequirementForCompany(It.Is<Guid>(y => y == codatCompanyId), It.IsAny<ApplicationDataRequirements>()),
            Times.Never);
        
        VerifyCodatClient();
        VerifyLoanUnderwriterIsNotCalled();
    }

    [Fact]
    public async Task UpdateDataTypeSyncStatusAsync_sets_application_status_to_CodatProcessingInProgress_when_data_requirements_are_not_met()
    {
        var codatCompanyId = Guid.NewGuid();
        var dataConnectionId = Guid.NewGuid();

        var application = new Application
        {
            Id = Guid.NewGuid(),
            CodatCompanyId = codatCompanyId,
            AccountingConnection = dataConnectionId,
            Requirements = { ApplicationDataRequirements.AccountsClassified }
        };
        
        _applicationStore.Setup(x => x.GetApplicationByCompanyId(It.IsAny<Guid>()))
            .Returns(application)
            .Verifiable();

        _applicationStore
            .Setup(x => x.AddFulfilledRequirementForCompany(It.IsAny<Guid>(), It.IsAny<ApplicationDataRequirements>()))
            .Verifiable();
        
        _applicationStore.Setup(x => x.GetApplication(It.Is<Guid>(y => y == application.Id)))
            .Returns(application)
            .Verifiable();

        _applicationStore.Setup(x => 
            x.UpdateApplicationStatus(It.Is<Guid>(y => y == application.Id), It.Is<ApplicationStatus>(y => y == ApplicationStatus.CollectingData)))
            .Verifiable();

        _applicationStore.Setup(x => x.GetApplicationStatus(It.Is<Guid>(y => y == application.Id)))
            .Returns(ApplicationStatus.CollectingData)
            .Verifiable();

        var alert = CreateDataSyncCompleteAlert(codatCompanyId, dataConnectionId, "chartOfAccounts");

        await _orchestrator.UpdateDataTypeSyncStatusAsync(alert);
        
        VerifyApplicationStore();
        VerifyCodatClient();
        VerifyLoanUnderwriterIsNotCalled();
    }
    
    [Theory]
    [InlineData("chartOfAccounts", ApplicationDataRequirements.ChartOfAccounts)]
    [InlineData("balanceSheet", ApplicationDataRequirements.BalanceSheet)]
    [InlineData("profitAndLoss", ApplicationDataRequirements.ProfitAndLoss)]
    public async Task UpdateDataTypeSyncStatusAsync_sets_application_status_to_CodatProcessingComplete_when_data_requirements_are_met(string dataType, ApplicationDataRequirements requirement)
    {
        var codatCompanyId = Guid.NewGuid();
        var dataConnectionId = Guid.NewGuid();
        var underwritingOutcome = ApplicationStatus.Accepted;

        var application = new Application
        {
            Id = Guid.NewGuid(),
            CodatCompanyId = codatCompanyId,
            AccountingConnection = dataConnectionId,
            Form = new()
            {
                LoanAmount = 10000m,
                LoanTerm = 36
            }
        };
        application.Requirements.AddRange(Enum.GetValues(typeof(ApplicationDataRequirements)).Cast<ApplicationDataRequirements>());
        
        _applicationStore.Setup(x => x.GetApplicationByCompanyId(It.Is<Guid>(y => y == codatCompanyId)))
            .Returns(application)
            .Verifiable();

        _applicationStore
            .Setup(x => x.AddFulfilledRequirementForCompany(It.Is<Guid>(y => y == codatCompanyId), It.Is<ApplicationDataRequirements>(y => y == requirement)))
            .Verifiable();
        
        _applicationStore.Setup(x => x.GetApplication(It.Is<Guid>(y => y == application.Id)))
            .Returns(application)
            .Verifiable();

        var applicationStatuses = new[] { ApplicationStatus.DataCollectionComplete, ApplicationStatus.Underwriting, underwritingOutcome };
        foreach (var status in applicationStatuses)
        {
            _applicationStore
                .Setup(x => 
                    x.UpdateApplicationStatus(It.Is<Guid>(y => y == application.Id), It.Is<ApplicationStatus>(y => y == status)))
                .Verifiable();
        }
        
        _applicationStore.Setup(x => x.GetApplicationStatus(It.Is<Guid>(y => y == application.Id)))
            .Returns(ApplicationStatus.DataCollectionComplete)
            .Verifiable();

        SetupCodatDataClientWithVerifiableFinancialRequests(application);
        
        _underwriter
            .Setup(x => x.Process(
                It.Is<decimal>(y => y == application.Form.LoanAmount),
                It.Is<int>(y => y == application.Form.LoanTerm),
                It.IsAny<Report>(), 
                It.IsAny<Report>()))
            .Returns(underwritingOutcome)
            .Verifiable();

        var alert = CreateDataSyncCompleteAlert(codatCompanyId, dataConnectionId, dataType);

        await _orchestrator.UpdateDataTypeSyncStatusAsync(alert);
        
        VerifyApplicationStore();
        VerifyCodatClient();

        _underwriter.Verify(x => 
                x.Process(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<Report>(), It.IsAny<Report>()), 
            Times.Once);
    }

    [Fact]
    public async Task UpdateAccountCategorisationStatusAsync_sets_accounts_classified_when_alert_is_received_and_all_accounts_are_categorised()
    {
        var codatCompanyId = Guid.NewGuid();
        var dataConnectionId = Guid.NewGuid();
        var underwritingOutcome = ApplicationStatus.Accepted;

        var application = new Application
        {
            Id = Guid.NewGuid(),
            CodatCompanyId = codatCompanyId,
            AccountingConnection = dataConnectionId,
            Form = new()
            {
                LoanAmount = 10000m,
                LoanTerm = 36
            }
        };
        application.Requirements.AddRange(Enum.GetValues(typeof(ApplicationDataRequirements)).Cast<ApplicationDataRequirements>());
        
        _applicationStore.Setup(x => x.GetApplicationByCompanyId(It.Is<Guid>(y => y == codatCompanyId)))
            .Returns(application)
            .Verifiable();

        _applicationStore
            .Setup(x => 
                x.AddFulfilledRequirementForCompany(It.Is<Guid>(y => y == codatCompanyId), It.Is<ApplicationDataRequirements>(y => y == ApplicationDataRequirements.AccountsClassified)))
            .Verifiable();
        
        _applicationStore.Setup(x => x.GetApplication(It.Is<Guid>(y => y == application.Id)))
            .Returns(application)
            .Verifiable();
        
        _applicationStore.Setup(x => x.GetApplicationStatus(It.Is<Guid>(y => y == application.Id)))
            .Returns(ApplicationStatus.DataCollectionComplete)
            .Verifiable();

        var applicationStatuses = new[] { ApplicationStatus.DataCollectionComplete, ApplicationStatus.Underwriting, underwritingOutcome };
        foreach (var status in applicationStatuses)
        {
            _applicationStore
                .Setup(x => 
                    x.UpdateApplicationStatus(It.Is<Guid>(y => y == application.Id), It.Is<ApplicationStatus>(y => y == status)))
                .Verifiable();
        }
        
        SetupCodatDataClientWithMetrics(application);
        SetupCodatDataClientWithVerifiableFinancialRequests(application);
        
        _underwriter
            .Setup(x => x.Process(
                It.Is<decimal>(y => y == application.Form.LoanAmount),
                It.Is<int>(y => y == application.Form.LoanTerm),
                It.IsAny<Report>(),
                It.IsAny<Report>()))
            .Returns(underwritingOutcome)
            .Verifiable();
        
        var alert = new CodatAccountCategorisationAlert
        {
            CompanyId = codatCompanyId
        };

        await _orchestrator.UpdateAccountCategorisationStatusAsync(alert);
        
        VerifyApplicationStore();
        VerifyCodatClient();
    }

    private void SetupCodatDataClientWithMetrics(Application application)
    => _codatDataClient
            .Setup(x => x.GetPreviousTwelveMonthsMetricsAsync(
                It.Is<Guid>(y => y == application.CodatCompanyId),
                It.Is<Guid>(y => y == application.AccountingConnection),
                It.Is<DateTime>(y => y == application.DateCreated)))
            .ReturnsAsync(new FinancialMetrics())
            .Verifiable();

    private void SetupCodatDataClientWithVerifiableFinancialRequests(Application application)
    {
        _codatDataClient
            .Setup(x => x.GetPreviousTwelveMonthsEnhancedProfitAndLossAsync(
                It.Is<Guid>(y => y == application.CodatCompanyId),
                It.Is<Guid>(y => y == application.AccountingConnection),
                It.Is<DateTime>(y => y == application.DateCreated)))
            .ReturnsAsync(new Report())
            .Verifiable();
        
        _codatDataClient
            .Setup(x => x.GetPreviousTwelveMonthsEnhancedBalanceSheetAsync(
                It.Is<Guid>(y => y == application.CodatCompanyId),
                It.Is<Guid>(y => y == application.AccountingConnection),
                It.Is<DateTime>(y => y == application.DateCreated)))
            .ReturnsAsync(new Report())
            .Verifiable();
    }
    
    private static CodatDataConnectionStatusAlert CreateDataConnectionStatusAlert(Guid companyId, string newStatus, string platformKey)
        => new()
        {
            CompanyId = companyId,
            Data = new CodatDataConnectionStatusData
            {
                DataConnectionId = Guid.NewGuid(),
                NewStatus = newStatus,
                PlatformKey = platformKey
            }
        };

    private static CodatDataSyncCompleteAlert CreateDataSyncCompleteAlert(Guid companyId, Guid? dataConnectionId = null, string dataType = "dataType")
        => new()
        {
            CompanyId = companyId,
            DataConnectionId = dataConnectionId ?? Guid.NewGuid(),
            Data = new CodatDataSyncCompleteData
            {
                DataType = dataType
            }
        };

    private void VerifyApplicationStore()
    {
        _applicationStore.Verify();
        _applicationStore.VerifyNoOtherCalls();
    }
    
    private void VerifyCodatClient()
    {
        _codatDataClient.Verify();
        _codatDataClient.VerifyNoOtherCalls();
    }

    private void VerifyLoanUnderwriterIsNotCalled()
        => _underwriter.Verify(x => 
                x.Process(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<Report>(), It.IsAny<Report>()), 
            Times.Never);
}