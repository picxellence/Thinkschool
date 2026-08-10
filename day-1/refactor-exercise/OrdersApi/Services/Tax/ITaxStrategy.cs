using System;

namespace LegacyApi.Services.Tax;

public interface ITaxStrategy
{
    bool CanHandle(string country);
    decimal CalculateTax(decimal orderTotal, string country);
}
