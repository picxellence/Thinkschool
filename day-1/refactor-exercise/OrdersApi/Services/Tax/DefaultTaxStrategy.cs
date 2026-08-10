using System;

namespace LegacyApi.Services.Tax;

public class DefaultTaxStrategy : ITaxStrategy
{
    public bool CanHandle(string country)
        => true;

    public decimal CalculateTax(decimal orderTotal, string country)
        => orderTotal * 0.15m;
}
