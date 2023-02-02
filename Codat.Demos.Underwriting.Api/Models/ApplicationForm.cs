using System.Text.Json.Serialization;

namespace Codat.Demos.Underwriting.Api.Models;

public enum ApplicationStatus
{
    Started,
    CollectingData,
    DataCollectionComplete,
    Underwriting,
    UnderwritingFailure,
    Accepted,
    Rejected
}

public enum ApplicationDataRequirements
{
    ApplicationDetails,
    ChartOfAccounts,
    BalanceSheet,
    ProfitAndLoss,
    AccountsClassified
}

public record ApplicationForm
{
    //Add loan purpose
    //remove ratios endpoint
    //front and back of house. 
    public Guid Id { get; init; }
    public DateTime DateCreated { get; } = DateTime.UtcNow.Date;
    public Guid CodatCompanyId { get; init; }
    public ApplicationStatus Status { get; init; }
    public string CompanyName { get; init; }
    public string FullName { get; init; }
    public decimal? LoanAmount { get; init; }
    public int? LoanTerm { get; init; }
    public string LoanPurpose { get; init; }
    
    public string LinkUrl => $"https://link.codat.io/company/{CodatCompanyId}";

    [JsonIgnore]
    public DataConnection? AccountingConnection { get; init; }//Change to guid.
    
    [JsonIgnore]
    public List<ApplicationDataRequirements> Requirements { get; } = new();
}