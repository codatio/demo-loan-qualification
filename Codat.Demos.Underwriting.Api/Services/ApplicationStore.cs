using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;

namespace Codat.Demos.Underwriting.Api.Services;

public interface IApplicationStore
{
    ApplicationForm CreateApplication(Guid applicationId, Guid codatCompanyId);
    ApplicationForm GetApplication(Guid id);
    void SetApplicationDetails(Guid applicationId, string companyName, string fullName, string loanPurpose, decimal loanAmount, int loanTerm);
    ApplicationStatus GetApplicationStatus(Guid id);
    void UpdateApplicationStatus(Guid id, ApplicationStatus status);
    void SetAccountingConnectionForCompany(Guid companyId, Guid dataConnectionId);
    ApplicationForm GetApplicationByCompanyId(Guid companyId);
    void AddFulfilledRequirement(Guid id, ApplicationDataRequirements requirement);
    void AddFulfilledRequirementForCompany(Guid companyId, ApplicationDataRequirements requirement);
}

public class ApplicationStore : IApplicationStore
{
    private readonly Dictionary<Guid, ApplicationForm> _data = new();
    public ApplicationForm CreateApplication(Guid applicationId, Guid codatCompanyId)
    {
        var applicationForm = new ApplicationForm { Id = applicationId, CodatCompanyId = codatCompanyId, Status = ApplicationStatus.Started };
        _data.Add(applicationForm.Id, applicationForm);
        return applicationForm;
    }

    public void SetApplicationDetails(Guid applicationId, string companyName, string fullName, string loanPurpose, decimal loanAmount, int loanTerm)
    {
        var application = GetApplication(applicationId);
        
        _data[application.Id] = application with
        {
            CompanyName = companyName,
            FullName = fullName,
            LoanPurpose = loanPurpose,
            LoanAmount = loanAmount,
            LoanTerm = loanTerm
        };
    }

    public ApplicationForm GetApplication(Guid id)
        => _data.TryGetValue(id, out var result) ? result : throw new ApplicationStoreException($"No application exists with id {id}");
    
    public ApplicationStatus GetApplicationStatus(Guid id)
        => GetApplication(id).Status;

    public void SetAccountingConnectionForCompany(Guid companyId, Guid dataConnectionId)
    {
        var application = GetApplicationByCompanyId(companyId);

        _data[application.Id] = application with
        {
            AccountingConnection = new DataConnection { Id = dataConnectionId }
        };
    }
    
    public void UpdateApplicationStatus(Guid id, ApplicationStatus status)
    {
        var application = GetApplication(id);
        _data[application.Id] = application with
        {
            Status = status
        };
    }

    public ApplicationForm GetApplicationByCompanyId(Guid companyId)
    {
        var applicationForm = _data.Values.FirstOrDefault(x => x.CodatCompanyId == companyId);
        if (applicationForm is null)
        {
            throw new ApplicationStoreException($"No application exists for codat company id {companyId}");
        }

        return applicationForm;
    }

    public void AddFulfilledRequirement(Guid id, ApplicationDataRequirements requirement)
    {
        var application = GetApplication(id);
        AddToRequirements(application.Id, requirement);
    }

    public void AddFulfilledRequirementForCompany(Guid companyId, ApplicationDataRequirements requirement)
    {
        var application = GetApplicationByCompanyId(companyId);
        AddToRequirements(application.Id, requirement);
    }

    private void AddToRequirements(Guid id, ApplicationDataRequirements requirement)
    {
        if (!_data[id].Requirements.Exists(x => x == requirement))
        {
            _data[id].Requirements.Add(requirement);
        }
    }
}