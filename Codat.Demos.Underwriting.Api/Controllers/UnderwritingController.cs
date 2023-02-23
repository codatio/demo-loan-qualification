using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Extensions;
using Codat.Demos.Underwriting.Api.Models;
using Codat.Demos.Underwriting.Api.Orchestrators;
using Microsoft.AspNetCore.Mvc;

namespace Codat.Demos.Underwriting.Api.Controllers;

[Route("applications/")]
[ApiController]
public class UnderwritingController : ControllerBase
{
    private readonly IApplicationOrchestrator _applicationOrchestrator;
    
    public UnderwritingController(IApplicationOrchestrator applicationOrchestrator)
    {
        _applicationOrchestrator = applicationOrchestrator;
    }
    
    /// <summary>
    /// Start a new loan application.
    /// </summary>
    /// <returns>A new application ID and codat company ID.</returns>
    [HttpGet]
    [Route("start")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApplicationForm), 200)]
    public async Task<OkObjectResult> StartApplicationAsync()
    {
        var applicationForm = await _applicationOrchestrator.CreateApplicationAsync();
        return Ok(applicationForm);
    }

    [HttpPost]
    [Route("form")]
    public async Task<IActionResult> SubmitFormAsync([FromBody] ApplicationForm form)
    {
        if (!IsApplicationValid(form))
        {
            return BadRequest();
        }

        ApplicationForm application;
        try
        {
            application = _applicationOrchestrator.GetApplication(form.Id);
        }
        catch (ApplicationOrchestratorException)
        {
            return NotFound(new ErrorResponse($"No application found with id {form.Id}."));
        }
        
        if (application.Requirements.Exists(x => x == ApplicationDataRequirements.ApplicationDetails))
        {
            return BadRequest(new ErrorResponse("Application details have already been received."));
        }

        await _applicationOrchestrator.SubmitApplicationDetailsAsync(form);
        return Ok();
    }
    
    [HttpGet]
    [Route("{applicationId}")]
    public IActionResult GetApplication([FromRoute]Guid applicationId)
    {
        try
        {
            var application =  _applicationOrchestrator.GetApplication(applicationId);
            return Ok(application);
        }
        catch (ApplicationOrchestratorException)
        {
            return NotFound(new ErrorResponse($"No application found with id {applicationId}."));
        }
    }

    private static bool IsApplicationValid(ApplicationForm form)
        => form.CompanyName.IsNotNullOrWhitespace() && 
           form.FullName.IsNotNullOrWhitespace() &&
           form.LoanPurpose.IsNotNullOrWhitespace() &&
           form.LoanAmount is > 0M &&
           form.LoanTerm is >= 12;
    
    private record ErrorResponse(string Message);
}

