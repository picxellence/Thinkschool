using System;

namespace LegacyApi.Services.Tax;

public class UsTaxStrategy : ITaxStrategy
{
    public bool CanHandle(string country)
        => string.Equals(country?.Trim(), "US", StringComparison.OrdinalIgnoreCase);

    public decimal CalculateTax(decimal orderTotal, string country)
        => orderTotal * 0.08m;
}
