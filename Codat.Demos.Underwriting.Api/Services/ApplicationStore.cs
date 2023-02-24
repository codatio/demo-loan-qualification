using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;

namespace Codat.Demos.Underwriting.Api.Services;

public interface IApplicationStore
{
    NewApplicationDetails CreateApplication(Guid applicationId, Guid codatCompanyId);
    Application GetApplication(Guid id);
    void SetApplicationForm(Guid applicationId, ApplicationForm form);
    ApplicationStatus GetApplicationStatus(Guid id);
    void UpdateApplicationStatus(Guid id, ApplicationStatus status);
    void SetAccountingConnectionForCompany(Guid companyId, Guid dataConnectionId);
    Application GetApplicationByCompanyId(Guid companyId);
    void AddFulfilledRequirement(Guid id, ApplicationDataRequirements requirement);
    void AddFulfilledRequirementForCompany(Guid companyId, ApplicationDataRequirements requirement);
}

public class ApplicationStore : IApplicationStore
{
    private readonly Dictionary<Guid, Application> _data = new();
    public NewApplicationDetails CreateApplication(Guid applicationId, Guid codatCompanyId)
    {
        var applicationForm = new Application { Id = applicationId, CodatCompanyId = codatCompanyId, Status = ApplicationStatus.Started };
        _data.Add(applicationForm.Id, applicationForm);
        return applicationForm;
    }

    public void SetApplicationForm(Guid applicationId, ApplicationForm form)
    {
        var application = GetApplication(applicationId);
        
        _data[application.Id] = application with
        {
            Form = form
        };
    }

    public Application GetApplication(Guid id)
        => _data.TryGetValue(id, out var result) ? result : throw new ApplicationStoreException($"No application exists with id {id}");
    
    public ApplicationStatus GetApplicationStatus(Guid id)
        => GetApplication(id).Status;

    public void SetAccountingConnectionForCompany(Guid companyId, Guid dataConnectionId)
    {
        var application = GetApplicationByCompanyId(companyId);

        _data[application.Id] = application with
        {
            AccountingConnection = dataConnectionId
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

    public Application GetApplicationByCompanyId(Guid companyId)
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