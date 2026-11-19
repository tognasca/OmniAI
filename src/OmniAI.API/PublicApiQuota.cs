using System.Threading.RateLimiting;

namespace OmniAI.API;

public sealed class PublicApiQuota : IDisposable
{
    private readonly PartitionedRateLimiter<(string Id, int Limit)> limiter =
        PartitionedRateLimiter.Create<(string Id, int Limit), (string Id, int Limit)>(
            key => RateLimitPartition.GetFixedWindowLimiter(key, partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = partition.Limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    public RateLimitLease Acquire(string id, int limit) => limiter.AttemptAcquire((id.ToUpperInvariant(), limit));

    public void Dispose() => limiter.Dispose();
}
