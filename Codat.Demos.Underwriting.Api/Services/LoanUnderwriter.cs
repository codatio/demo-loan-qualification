using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;
using Microsoft.Extensions.Options;

namespace Codat.Demos.Underwriting.Api.Services;

public interface ILoanUnderwriter
{
    ApplicationStatus Process(decimal loanAmount, int loanTerm, FinancialStatement profitAndLoss, FinancialStatement balanceSheet);
}

public class LoanUnderwriter : ILoanUnderwriter
{
    private readonly UnderwritingParameters _parameters;

    public LoanUnderwriter(IOptions<UnderwritingParameters> options)
    {
        _parameters = options.Value;
    }
    
    public ApplicationStatus Process(decimal loanAmount, int loanTerm, FinancialStatement profitAndLoss, FinancialStatement balanceSheet)
    {
        try
        {
            var profitMarginAcceptable = IsGrossProfitMarginThresholdPassed(profitAndLoss);
            var revenueAssessmentPassed = IsRevenueThresholdPassed(profitAndLoss, loanAmount, loanTerm);
            var gearingPassed = IsGearingRatioBelowThreshold(balanceSheet);

            return profitMarginAcceptable && revenueAssessmentPassed && gearingPassed ? ApplicationStatus.Accepted : ApplicationStatus.Rejected;
        }
        catch (Exception ex) when (ex is LoanUnderwriterException or InvalidOperationException or ArgumentNullException)
        {
            return ApplicationStatus.UnderwritingFailure;
        }
    }
    
    private bool IsGrossProfitMarginThresholdPassed(FinancialStatement profitAndLoss)
    {
        var netSales = profitAndLoss.Lines.Where(x => x.AccountCategorization.StartsWith("Income.Operating")).Sum(x => x.Balance);
        var costOfSales = profitAndLoss.Lines.Where(x => x.AccountCategorization.StartsWith("Expense.CostOfSales")).Sum(x => x.Balance); 
        
        var grossProfit = netSales - costOfSales;
        var grossProfitMargin = netSales == 0 ? 0 : grossProfit / netSales;
        return _parameters.MinGrossProfitMargin < grossProfitMargin;
    }

    private bool IsRevenueThresholdPassed(FinancialStatement profitAndLoss, decimal loanAmount, int loanTerm)
    {
        var operatingIncome = profitAndLoss.Lines.Where(x => x.AccountCategorization.StartsWith("Income.Operating")).Sum(x => x.Balance);
        var totalLoanAmount = loanAmount * (1 + _parameters.LoanCommissionPercentage);

        var monthlyRevenue = operatingIncome / 12;
        var monthlyLoanPayment = totalLoanAmount / loanTerm;

        var revenuePercentage = monthlyRevenue == 0 ? 0 : monthlyLoanPayment / monthlyRevenue;

        return revenuePercentage < _parameters.RevenueThreshold;
    }

    private bool IsGearingRatioBelowThreshold(FinancialStatement balanceSheet)
    {
        var totalAssets = balanceSheet.Lines.Where(x => x.AccountCategorization.StartsWith("Asset")).Sum(x => x.Balance);
        var totalDebt = balanceSheet.Lines.Where(x => x.AccountCategorization.StartsWith("Liability.NonCurrent.LoansPayable")).Sum(x => x.Balance);
        var gearingRatio = totalAssets == 0 ? 0m : totalDebt/totalAssets;

        return gearingRatio <= _parameters.MaxGearingRatio;
    }
}