using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SadcOMS.Domain.Events;
using SadcOMS.Infrastructure.Messaging;
using SadcOMS.Infrastructure.Repositories;

namespace SadcOMS.Worker.Services;

public class OutboxOptions
{
    public const string SectionName = "Outbox";
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public int MaxRetries { get; set; } = 5;
}

public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqPublisher _publisher;
    private readonly OutboxOptions _options;
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly Dictionary<Guid, int> _retryCounts = new();

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IRabbitMqPublisher publisher,
        IOptions<OutboxOptions> options,
        IOptions<RabbitMqOptions> rabbitOptions,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _options = options.Value;
        _rabbitOptions = rabbitOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox batch");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var messages = await outboxRepo.GetUnprocessedBatchAsync(_options.BatchSize, ct);

        foreach (var message in messages)
        {
            try
            {
                if (message.Type == OutboxMessageTypes.OrderCreated)
                {
                    var payload = JsonSerializer.Deserialize<OrderCreatedEvent>(message.Payload);
                    if (payload is not null)
                    {
                        await _publisher.PublishAsync(
                            _rabbitOptions.OrderCreatedRoutingKey, payload, ct);
                    }
                }

                await outboxRepo.MarkProcessedAsync(message.Id, ct);
                await outboxRepo.SaveChangesAsync(ct);
                _retryCounts.Remove(message.Id);
                _logger.LogInformation("Outbox message {MessageId} published", message.Id);
            }
            catch (Exception ex)
            {
                _retryCounts.TryGetValue(message.Id, out var retries);
                retries++;
                _retryCounts[message.Id] = retries;

                var error = $"Attempt {retries}: {ex.Message}";
                await outboxRepo.MarkFailedAsync(message.Id, error, ct);
                await outboxRepo.SaveChangesAsync(ct);

                _logger.LogWarning(ex, "Failed to publish outbox message {MessageId}, attempt {Retry}",
                    message.Id, retries);

                if (retries < _options.MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retries));
                    await Task.Delay(delay, ct);
                }
            }
        }
    }
}
