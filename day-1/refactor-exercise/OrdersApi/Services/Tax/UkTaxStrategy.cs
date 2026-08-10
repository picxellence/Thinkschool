using System;

namespace LegacyApi.Services.Tax;

public class UkTaxStrategy : ITaxStrategy
{
    public bool CanHandle(string country)
        => string.Equals(country?.Trim(), "UK", StringComparison.OrdinalIgnoreCase);

    public decimal CalculateTax(decimal orderTotal, string country)
        => orderTotal * 0.20m;
}
