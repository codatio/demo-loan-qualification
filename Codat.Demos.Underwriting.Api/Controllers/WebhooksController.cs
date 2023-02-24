using System.Text.Json;
using Codat.Demos.Underwriting.Api.Models;
using Codat.Demos.Underwriting.Api.Orchestrators;
using Microsoft.AspNetCore.Mvc;

namespace Codat.Demos.Underwriting.Api.Controllers;

[ApiController]
[Route("webhooks/codat/")]
public class WebhooksController : ControllerBase
{
    private readonly IApplicationOrchestrator _applicationOrchestrator;
    
    public WebhooksController(IApplicationOrchestrator orchestrator)
    {
        _applicationOrchestrator = orchestrator;
    }
    
    /// <summary>
    /// Webhook receiver listening to completed syncs events for each data type (Rule name: Data sync completed).
    /// </summary>
    /// <response code="200">Successfully processed webhook.</response>
    [HttpPost]
    [Route("datatype-sync-complete")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> NotificationOfDataTypeSyncCompleteAsync([FromBody]CodatDataSyncCompleteAlert alert)
    {
        Console.WriteLine("datatype-sync-complete");
        await _applicationOrchestrator.UpdateDataTypeSyncStatusAsync(alert);
        var message = JsonSerializer.Serialize(alert);
        Console.WriteLine(message);
        return Ok();
    }
    
    /// <summary>
    /// Webhook receiver listening to changes to data connections (Rule name: Company Data Connection status has changed).
    /// </summary>
    /// <response code="200">Successfully processed webhook.</response>
    [HttpPost]
    [Route("data-connection-status")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> NotificationOfDataConnectionStatusChangeAsync([FromBody]CodatDataConnectionStatusAlert alert)
    {
        Console.WriteLine("data-connection-status");
        var message = JsonSerializer.Serialize(alert);
        Console.WriteLine(message);
        await _applicationOrchestrator.UpdateCodatDataConnectionAsync(alert);
        return Ok();
    }
    
    /// <summary>
    /// Webhook receiver listening to updates to account categorisation (Rule name: Account categories updated).
    /// </summary>
    /// <response code="200">Successfully processed webhook.</response>
    [HttpPost]
    [Route("account-categorisation-update")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> NotificationOfAccountCategorisationUpdatedAsync([FromBody]CodatAccountCategorisationAlert alert)
    {
        Console.WriteLine("account-categorisation-update");
        var message = JsonSerializer.Serialize(alert);
        Console.WriteLine(message);
        await _applicationOrchestrator.UpdateAccountCategorisationStatusAsync(alert);
        return Ok();
    }
}