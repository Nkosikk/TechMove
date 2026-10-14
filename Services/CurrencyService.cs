using System.Net.Http.Json;

namespace TechMove.Services;

public class CurrencyService(HttpClient httpClient) : ICurrencyService
{
    public async Task<decimal> GetUsdToZarRateAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<ExchangeRateResponse>(
            "latest?from=USD&to=ZAR", cancellationToken);

        if (response?.Rates.TryGetValue("ZAR", out var rate) != true || rate <= 0)
        {
            throw new InvalidOperationException("The currency service returned an invalid USD-to-ZAR rate.");
        }

        return rate;
    }

    public decimal CalculateZar(decimal amountUsd, decimal usdToZarRate)
    {
        if (amountUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountUsd));
        }

        if (usdToZarRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usdToZarRate));
        }

        return decimal.Round(amountUsd * usdToZarRate, 2, MidpointRounding.AwayFromZero);
    }

    private sealed record ExchangeRateResponse(Dictionary<string, decimal> Rates);
}
