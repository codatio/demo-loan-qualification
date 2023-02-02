using Codat.Demos.Underwriting.Api.Exceptions;
using Codat.Demos.Underwriting.Api.Models;
using Microsoft.Extensions.Options;

namespace Codat.Demos.Underwriting.Api.Services;

public interface ILoanUnderwriter
{
    ApplicationStatus Process(decimal loanAmount, int loanTerm, Report profitAndLoss, Report balanceSheet);
}

public class LoanUnderwriter : ILoanUnderwriter
{
    private readonly UnderwritingParameters _parameters;

    public LoanUnderwriter(IOptions<UnderwritingParameters> options)
    {
        _parameters = options.Value;
    }
    
    public ApplicationStatus Process(decimal loanAmount, int loanTerm, Report profitAndLoss, Report balanceSheet)
    {
        if (profitAndLoss.ReportData.Length != 1)
        {
            throw new LoanUnderwriterException("Profit and loss report does not contain exactly one period");
        }
        
        if (balanceSheet.ReportData.Length != 1)
        {
            throw new LoanUnderwriterException("Balance sheet report does not contain exactly one period");
        }

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
    
    private bool IsGrossProfitMarginThresholdPassed(Report profitAndLoss)
    {
        var operatingIncomeComponent = profitAndLoss.ReportData[0].Select("Income").Select("Operating");
        var netSales = GetMeasuresValue(operatingIncomeComponent, "Income.Operating");
        var costOfSalesComponent = profitAndLoss.ReportData[0].Select("Expense").Select("CostOfSales");
        var costOfSales =  GetMeasuresValue(costOfSalesComponent, "Expense.CostOfSales");
        
        var grossProfit = netSales - costOfSales;
        var grossProfitMargin = netSales == 0 ? 0 : grossProfit / netSales;
        return _parameters.MinGrossProfitMargin < grossProfitMargin;
    }

    private bool IsRevenueThresholdPassed(Report profitAndLoss, decimal loanAmount, int loanTerm)
    {
        var income = GetReportComponent(profitAndLoss.ReportData[0].Components, "Income");
        var operatingIncomeComponent = GetReportComponent(income.Components, "Operating");
        if (operatingIncomeComponent.Measures is null || operatingIncomeComponent.Measures.Count != 1)
        {
            throw new LoanUnderwriterException($"Unexpected number of measures for Income.Operating");
        }

        var operatingIncome = operatingIncomeComponent.Measures[0].Value;

        var totalLoanAmount = loanAmount * (1 + _parameters.LoanCommissionPercentage);

        var monthlyRevenue = operatingIncome / 12;
        var monthlyLoanPayment = totalLoanAmount / loanTerm;

        var revenuePercentage = monthlyRevenue == 0 ? 0 : monthlyLoanPayment / monthlyRevenue;

        return revenuePercentage < _parameters.RevenueThreshold;
    }

    private bool IsGearingRatioBelowThreshold(Report balanceSheet)
    {
        var assets = GetReportComponent(balanceSheet.ReportData[0].Components, "Asset");
        if (assets.Measures is null || assets.Measures.Count != 1)
        {
            throw new LoanUnderwriterException($"Unexpected number of measures for Asset");
        }

        var totalAssets = assets.Measures[0].Value;

        var totalLongTermDebt = balanceSheet.ReportData[0].Select("Liability").Select( "NonCurrent").Select("LoansPayable");
        var totalDebt = GetMeasuresValue(totalLongTermDebt, "Liability.NonCurrent.LoansPayable"); //totalLongTermDebt.Measures[0].Value;
        
        var gearingRatio = totalAssets == 0 ? 0m : totalDebt/totalAssets;

        return gearingRatio <= _parameters.MaxGearingRatio;
    }

    private static Component GetReportComponent(IEnumerable<Component> components, string itemDisplayName) 
        => components.Single(x => x.ItemDisplayName.Equals(itemDisplayName));

    private static decimal GetMeasuresValue(Component component, string componentName)
    {
        if (component.Measures is null || component.Measures.Count != 1)
        {
            throw new LoanUnderwriterException($"Unexpected number of measures for {componentName}");
        }

        return component.Measures[0].Value;
    }
}