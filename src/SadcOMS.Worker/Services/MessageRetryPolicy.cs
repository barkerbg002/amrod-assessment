using System.Text;

namespace SadcOMS.Worker.Services;

/// <summary>
/// Consumer-side retry decisions for OrderCreated messages.
/// Republish-with-header is used instead of BasicNack(requeue:true) because RabbitMQ
/// does not mutate headers on requeue — x-retry-count must be incremented explicitly.
/// </summary>
public static class MessageRetryPolicy
{
    public const int MaxRetryCount = 3;

    public static int GetRetryCount(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-retry-count", out var value))
            return 0;

        return value switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes => int.TryParse(Encoding.UTF8.GetString(bytes), out var n) ? n : 0,
            _ => 0
        };
    }

    /// <summary>
    /// Returns true when the message has exhausted retries and should be dead-lettered.
    /// Count 0–2 are retried (republished with incremented header); count 3+ goes to DLQ.
    /// </summary>
    public static bool ShouldDeadLetter(int currentRetryCount) =>
        currentRetryCount >= MaxRetryCount;

    public static int NextRetryCount(int currentRetryCount) => currentRetryCount + 1;
}
