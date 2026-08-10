using System.Collections.Generic;
using LegacyApi.Services.Tax;
using Xunit;

namespace OrdersApi.Tests;

public class TaxStrategyTests
{
    [Fact]
    public void UsTaxStrategy_calculates_8_percent_tax()
    {
        var strategy = new UsTaxStrategy();
        var tax = strategy.CalculateTax(100m, "US");

        Assert.Equal(8m, tax);
    }

    [Fact]
    public void UkTaxStrategy_calculates_20_percent_tax()
    {
        var strategy = new UkTaxStrategy();
        var tax = strategy.CalculateTax(100m, "UK");

        Assert.Equal(20m, tax);
    }

    [Fact]
    public void DefaultTaxStrategy_calculates_15_percent_tax_for_other_countries()
    {
        var strategy = new DefaultTaxStrategy();
        var tax = strategy.CalculateTax(100m, "CA");

        Assert.Equal(15m, tax);
    }

    [Fact]
    public void TaxStrategyProvider_returns_correct_strategy_by_country()
    {
        var strategies = new List<ITaxStrategy>
        {
            new UsTaxStrategy(),
            new UkTaxStrategy(),
            new DefaultTaxStrategy()
        };

        var provider = new TaxStrategyProvider(strategies);

        Assert.IsType<UsTaxStrategy>(provider.GetStrategy("US"));
        Assert.IsType<UkTaxStrategy>(provider.GetStrategy("UK"));
        Assert.IsType<DefaultTaxStrategy>(provider.GetStrategy("CA"));
    }
}