namespace LegacyApi.Services.Tax;

public interface ITaxStrategyProvider
{
    ITaxStrategy GetStrategy(string country);
}
