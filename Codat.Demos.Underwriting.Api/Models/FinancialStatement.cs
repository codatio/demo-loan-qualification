namespace Codat.Demos.Underwriting.Api.Models;

public enum FinancialStatementType
{
    ProfitAndLoss,
    BalanceSheet
}

public record FinancialStatement
{
    public FinancialStatementType Type { init; get; }
    public FinancialStatementLine[] Lines { init; get; }
}

public record FinancialStatementLine
{
    public string AccountCategorization { init; get; }
    public DateTime Date { init; get; }
    public decimal Balance { init; get; }
}
