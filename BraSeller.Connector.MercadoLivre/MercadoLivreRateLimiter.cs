using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace BraSeller.Connector.MercadoLivre;

public sealed class MercadoLivreRateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new();

    public async ValueTask AcquireAsync(string accessToken, CancellationToken cancellationToken)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));
        var limiter = _limiters.GetOrAdd(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 100,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 500,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        }.CreateLimiter());
        using var lease = await limiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
            throw new TimeoutException("Mercado Livre request rate limit queue is full.");
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values) limiter.Dispose();
        _limiters.Clear();
    }
}

internal static class RateLimiterOptionsExtensions
{
    public static TokenBucketRateLimiter CreateLimiter(this TokenBucketRateLimiterOptions options) => new(options);
}
