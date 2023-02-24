using Codat.Demos.Underwriting.Api.Exceptions;
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
    /// <returns>New application details such as the application ID and Codat Company ID.</returns>
    /// <response code="200">New application details.</response>
    [HttpGet]
    [Route("start")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(NewApplicationDetails), 200)]
    public async Task<NewApplicationDetails> StartApplicationAsync()
    {
        var newApplicationDetails = await _applicationOrchestrator.CreateApplicationAsync();
        return newApplicationDetails;
    }

    /// <summary>
    /// Submit application form for application ID. 
    /// </summary>
    /// <param name="applicationId">The loan application ID.</param>
    /// <param name="form">Details submitted by the applicant.</param>
    /// <returns>No response body.</returns>
    /// <response code="200">Successfully received application form.</response>
    /// <response code="404">No application exists for application ID.</response>
    /// <response code="400">Either validation failure or form already submitted for application ID.</response>
    [HttpPost]
    [Produces("application/json")]
    [Route("{applicationId}/form")]
    [ProducesResponseType(200)]
    [ProducesResponseType( 404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SubmitFormAsync([FromRoute]Guid applicationId, [FromBody] ApplicationForm form)
    {
        Application application;
        try
        {
            application = _applicationOrchestrator.GetApplication(applicationId);
        }
        catch (ApplicationOrchestratorException)
        {
            return NotFound();
        }
        
        if (application.Requirements.Exists(x => x == ApplicationDataRequirements.ApplicationDetails))
        {
            return BadRequest(new
            {
                type = "form-resubmit-error", 
                title = "Cannot resubmit application form.",
                detail = "Application details have already been received.",
            });
        }

        await _applicationOrchestrator.SubmitApplicationDetailsAsync(applicationId, form);
        return Ok();
    }
    
    /// <summary>
    /// Get application
    /// </summary>
    /// <param name="applicationId">The loan application ID.</param>
    /// <returns>Returns the application.</returns>
    /// <response code="200">Returns application.</response>
    /// <response code="404">No application exists for application ID.</response>
    [HttpGet]
    [Route("{applicationId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Application), 200)]
    [ProducesResponseType(404)]
    public IActionResult GetApplication([FromRoute]Guid applicationId)
    {
        try
        {
            var application =  _applicationOrchestrator.GetApplication(applicationId);
            return Ok(application);
        }
        catch (ApplicationOrchestratorException)
        {
            return NotFound();
        }
    }
}

