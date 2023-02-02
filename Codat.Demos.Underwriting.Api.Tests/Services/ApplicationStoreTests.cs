using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;
using Codat.Demos.Underwriting.Api.Services;
using FluentAssertions;
using Xunit;

namespace Codat.Demos.Underwriting.Api.Tests.Services;

public class ApplicationStoreTests
{
    private readonly ApplicationStore _applicationStore = new();
    private readonly ApplicationForm _applicationForm = new(){
        Id = Guid.NewGuid(),
        CodatCompanyId = Guid.NewGuid(),
        Status = ApplicationStatus.Started
    };

    [Fact]
    public void CreateApplication_sets_only_expected_fields()
    {
        _applicationStore.CreateApplication(_applicationForm.Id, _applicationForm.CodatCompanyId);
        var application = _applicationStore.GetApplication(_applicationForm.Id);
        application.Should().BeEquivalentTo(_applicationForm);
        application.Requirements.Should().BeEmpty();
    }

    [Fact]
    public void GetApplication_successfully_retrieves_application_when_multiple_exist()
    {
        var applications = new[]
        {
            _applicationForm,
            new()
            {
                Id = Guid.NewGuid(),
                CodatCompanyId = Guid.NewGuid(),
                Status = ApplicationStatus.Started
            }
        };

        foreach (var application in applications)
        {
            _applicationStore.CreateApplication(application.Id, application.CodatCompanyId);
        }

        foreach (var id in applications.Select(x => x.Id))
        {
            var application = _applicationStore.GetApplication(id);
            applications.Should().Contain(x => x.Id == application.Id);
        }
    }

    [Fact]
    public void SetApplicationDetails_sets_expected_fields()
    {
        var expectation = _applicationForm with
        {
            CompanyName = "Company Name",
            FullName = "First Last",
            LoanAmount = 10000M,
            LoanTerm = 36,
            LoanPurpose = "Growth marketing",
            Status = ApplicationStatus.Started
        };
        
        _applicationStore.CreateApplication(_applicationForm.Id, _applicationForm.CodatCompanyId);
        _applicationStore.SetApplicationDetails(_applicationForm.Id, expectation.CompanyName, expectation.FullName, expectation.LoanPurpose, expectation.LoanAmount.Value, expectation.LoanTerm.Value);
        var application = _applicationStore.GetApplication(_applicationForm.Id);
        application.Should().BeEquivalentTo(expectation);
    }

    [Fact]
    public void GetApplication_throws_ApplicationStoreException_when_no_application_exists()
    {
        var missingId = Guid.NewGuid();
        _applicationStore.CreateApplication(_applicationForm.Id, _applicationForm.CodatCompanyId);
        var action = () => _applicationStore.GetApplication(missingId);
        action.Should().Throw<ApplicationStoreException>().WithMessage($"No application exists with id {missingId}");
    }

    [Fact]
    public void GetApplicationByCompanyId_throws_ApplicationStoreException_when_no_company_exists()
    {
        var missingCompanyId = Guid.NewGuid();
        _applicationStore.CreateApplication(_applicationForm.Id, _applicationForm.CodatCompanyId);
        var action = () => _applicationStore.GetApplicationByCompanyId(missingCompanyId);
        action.Should().Throw<ApplicationStoreException>().WithMessage($"No application exists for codat company id {missingCompanyId}");
    }

    [Fact]
    public void SetRequirementForCompany_sets_requirements_as_expected()
    {
        var expectation = _applicationForm;

        _applicationStore.CreateApplication(_applicationForm.Id, _applicationForm.CodatCompanyId);

        foreach (var requirement in Enum.GetValues(typeof(ApplicationDataRequirements)).Cast<ApplicationDataRequirements>())
        {
            _applicationStore.AddFulfilledRequirementForCompany(_applicationForm.CodatCompanyId, requirement);
            var application = _applicationStore.GetApplication(_applicationForm.Id);
            expectation.Requirements.Add(requirement);
            application.Should().BeEquivalentTo(expectation);
        }
        _applicationForm.Requirements.Should().HaveSameCount(Enum.GetValues(typeof(ApplicationDataRequirements)).Cast<ApplicationDataRequirements>());
    }

    [Fact]
    public void Order_of_application_updates_do_not_change_fields_unexpectedly()
    {
        var expectation = _applicationForm;
        var requirement = ApplicationDataRequirements.BalanceSheet;
        expectation.Requirements.Add(requirement);
        _applicationStore.CreateApplication(_applicationForm.Id, _applicationForm.CodatCompanyId);
        _applicationStore.AddFulfilledRequirementForCompany(_applicationForm.CodatCompanyId, requirement);
        var application = _applicationStore.GetApplication(_applicationForm.Id);
        application.Should().BeEquivalentTo(expectation);

        expectation = expectation with
        {
            CompanyName = "Company Name",
            FullName = "First Last",
            LoanAmount = 10000M,
            LoanTerm = 36,
            LoanPurpose = "Growth marketing",
            Status = ApplicationStatus.Started
        };
        
        _applicationStore.SetApplicationDetails(_applicationForm.Id, expectation.CompanyName, expectation.FullName, expectation.LoanPurpose, expectation.LoanAmount.Value, expectation.LoanTerm.Value);
        application = _applicationStore.GetApplication(_applicationForm.Id);
        application.Should().BeEquivalentTo(expectation);
        
        expectation = expectation with
        {
            AccountingConnection = new DataConnection{ Id = Guid.NewGuid() }
        };
        
        _applicationStore.SetAccountingConnectionForCompany(_applicationForm.CodatCompanyId, expectation.AccountingConnection.Id);
        
        application = _applicationStore.GetApplication(_applicationForm.Id);
        application.Should().BeEquivalentTo(expectation);
    }
}