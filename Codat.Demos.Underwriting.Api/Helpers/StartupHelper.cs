using System.Text.Json;
using Codat.Demos.Underwriting.Api.DataClients;
using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Extensions;
using Codat.Demos.Underwriting.Api.Models;

namespace Codat.Demos.Underwriting.Api.Helpers;

// This class sets up and tears down Codat's rules (webhooks) used by this application.
// Note that no security (auth. header) is setup by default for this application but
// Codat's rules API can be configured with both basic and bearer auth.
public static class StartupHelper
{
    private static string FileName = "ruleIds.json";
    
    public static void SetupCodatWebhooks(WebApplication application)
    {
        var dataClient = application.Services.GetService(typeof(ICodatDataClient)) as ICodatDataClient;
    
        var webhookBaseUrlParam = "AppSettings:BaseWebhookUrl";
    
        var webhookBaseUrl = application.Configuration.GetSection(webhookBaseUrlParam).Value;
    
        if (webhookBaseUrl.IsNullOrWhitespace())
        {
            throw new ConfigurationMissingException(webhookBaseUrlParam);
        }
    
        //Send webhook urls to Codat
        application.Lifetime.ApplicationStarted.Register(() => OnApplicationStartedAsync(dataClient, webhookBaseUrl).GetAwaiter().GetResult());
        
        //Teardown: clean up webhooks created in Codat.
        application.Lifetime.ApplicationStopped.Register(() => OnApplicationStoppingAsync(dataClient).GetAwaiter().GetResult());
    }
    
    private static async Task OnApplicationStartedAsync(ICodatDataClient dataClient, string webhookBaseUrl)
    {
        //Called to remove old rules also called here just in case the application didn't shutdown gracefully.
        var removeOldRulesTask = TryRemoveOldRulesAsync(dataClient);
        
        var endpoints = new(string url, string type)[]
        {
            ("DataConnectionStatusChanged", "/webhooks/codat/data-connection-status"),
            ("Data sync completed", "/webhooks/codat/datatype-sync-complete"),
            ("account-categories-updated", "/webhooks/codat/account-categorisation-update")
        };
    
        var tasks = new List<Task<Rule>>();
        foreach (var (type, url) in endpoints)
        {
            tasks.Add(dataClient.CreateWebhookRuleAsync(type, webhookBaseUrl + url));
        }
        
        await Task.WhenAll(tasks);
        await removeOldRulesTask;
        
        var rules = tasks.Select(x => x.Result);
        var jsonString = JsonSerializer.Serialize(rules);
        await File.WriteAllTextAsync(FileName, jsonString);
    }

    private static Task OnApplicationStoppingAsync(ICodatDataClient dataClient)
        => TryRemoveOldRulesAsync(dataClient);

    private static Rule[] TryGetStoredRuleIds()
    {
        if (!File.Exists(FileName))
        {
            return Array.Empty<Rule>();
        }
        
        var jsonString = File.ReadAllText(FileName);
        var rules = JsonSerializer.Deserialize<Rule[]>(jsonString);
        return rules ?? Array.Empty<Rule>();
    }

    private static async Task TryRemoveOldRulesAsync(ICodatDataClient dataClient)
    {
        var oldRules = TryGetStoredRuleIds();
        if (oldRules.Length == 0)
        {
            return;
        }

        var deletionTasks = new List<Task>();
        foreach (var rule in oldRules)
        {
            deletionTasks.Add(dataClient.TryDeleteRuleAsync(rule.Id!.Value));
        }

        await Task.WhenAll(deletionTasks);
    }
}