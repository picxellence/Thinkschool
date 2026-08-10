using System.Collections.Generic;
using System.Linq;

namespace LegacyApi.Services.Tax;

public class TaxStrategyProvider : ITaxStrategyProvider
{
    private readonly IEnumerable<ITaxStrategy> _strategies;

    public TaxStrategyProvider(IEnumerable<ITaxStrategy> strategies)
    {
        _strategies = strategies;
    }

    public ITaxStrategy GetStrategy(string country)
    {
        return _strategies.FirstOrDefault(s => s.CanHandle(country))
            ?? throw new InvalidOperationException($"No tax strategy registered for country '{country}'");
    }
}
