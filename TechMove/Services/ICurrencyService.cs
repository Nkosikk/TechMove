namespace TechMove.Services;

public interface ICurrencyService
{
    Task<decimal> GetUsdToZarRateAsync(CancellationToken cancellationToken = default);
    decimal CalculateZar(decimal amountUsd, decimal usdToZarRate);
}
