using System.ComponentModel.DataAnnotations;
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

public record NewApplicationDetails
{
    /// <summary>
    /// Unique application identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The current status of the application.
    /// </summary>
    public ApplicationStatus Status { get; init; }
    
    /// <summary>
    /// Unique ID of the company created in Codat's system.
    /// </summary>
    public Guid CodatCompanyId { get; init; }
    
    /// <summary>
    /// Codat's hosted link URI. This allows applicants to connect their accounting platform.
    /// </summary>
    /// <example>https://link.codat.io/company/{codatCompanyId}</example>
    [MinLength(1)]
    public string LinkUrl => $"https://link.codat.io/company/{CodatCompanyId}";
}

public record ApplicationForm
{
    /// <summary>
    /// Name of company applying for a loan.
    /// </summary>
    /// <example>Toft stores</example>
    [Required]
    public string CompanyName { get; init; }
    
    /// <summary>
    /// Full name of the applicant applying on behalf of the company. 
    /// </summary>
    /// <example>John Doe</example>
    [Required]
    public string FullName { get; init; }
    
    /// <summary>
    /// Amount the applicant is applying for.
    /// </summary>
    /// <example>25000</example>
    [Required]
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal LoanAmount { get; init; }
    
    /// <summary>
    /// The requested duration of the loan.
    /// </summary>
    /// <example>12</example>
    [Required]
    [Range(12, int.MaxValue)]
    public int LoanTerm { get; init; }
    
    /// <summary>
    /// A description of the loan's purpose.
    /// </summary>
    /// <example>Growth marketing</example>
    [Required]
    public string LoanPurpose { get; init; }
}

public record Application : NewApplicationDetails
{
    [JsonIgnore]
    public DateTime DateCreated { get; } = DateTime.UtcNow.Date;
    
    public ApplicationForm? Form { get; set; }
    
    [JsonIgnore]
    public Guid? AccountingConnection { get; init; }
    
    [JsonIgnore]
    public List<ApplicationDataRequirements> Requirements { get; } = new();
}