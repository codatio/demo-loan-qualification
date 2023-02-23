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
    
    [HttpPost]
    [Route("datatype-sync-complete")]
    public async Task<IActionResult> NotificationOfDataTypeSyncCompleteAsync([FromBody]CodatDataSyncCompleteAlert alert)
    {
        Console.WriteLine("datatype-sync-complete");
        await _applicationOrchestrator.UpdateDataTypeSyncStatusAsync(alert);
        var message = JsonSerializer.Serialize(alert);
        Console.WriteLine(message);
        return Ok();
    }
    
    [HttpPost]
    [Route("data-connection-status")]
    public async Task<IActionResult> NotificationOfDataConnectionStatusChangeAsync([FromBody]CodatDataConnectionStatusAlert alert)
    {
        Console.WriteLine("data-connection-status");
        var message = JsonSerializer.Serialize(alert);
        Console.WriteLine(message);
        await _applicationOrchestrator.UpdateCodatDataConnectionAsync(alert);
        return Ok();
    }
    
    [HttpPost]
    [Route("account-categorisation-update")]
    public async Task<IActionResult> NotificationOfAccountCategorisationUpdatedAsync([FromBody]CodatAccountCategorisationAlert alert)
    {
        Console.WriteLine("account-categorisation-update");
        var message = JsonSerializer.Serialize(alert);
        Console.WriteLine(message);
        await _applicationOrchestrator.UpdateAccountCategorisationStatusAsync(alert);
        return Ok();
    }
}