using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SadcOMS.Domain.Events;
using SadcOMS.Infrastructure.Messaging;

namespace SadcOMS.Worker.Services;

public class OrderCreatedConsumer : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public OrderCreatedConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<OrderCreatedConsumer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(_options.OrderCreatedDlq, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(_options.OrderCreatedQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = _options.OrderCreatedDlq
            });
        _channel.QueueBind(_options.OrderCreatedQueue, _options.ExchangeName, _options.OrderCreatedRoutingKey);

        _channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;

        _channel.BasicConsume(_options.OrderCreatedQueue, autoAck: false, consumer);
        _logger.LogInformation("OrderCreatedConsumer started, listening on {Queue}", _options.OrderCreatedQueue);

        stoppingToken.Register(() =>
        {
            _logger.LogInformation("OrderCreatedConsumer stopping");
        });

        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());

        try
        {
            var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(body)
                ?? throw new InvalidOperationException("Failed to deserialize OrderCreatedEvent");

            _logger.LogInformation(
                "Processing OrderCreated for Order {OrderId}, Customer {CustomerId}, Total {Total} {Currency}",
                orderEvent.OrderId, orderEvent.CustomerId, orderEvent.TotalAmount, orderEvent.CurrencyCode);

            // Simulate fulfillment allocation
            await Task.Delay(TimeSpan.FromSeconds(2));

            _logger.LogInformation(
                "Fulfillment allocated for Order {OrderId} with {LineItemCount} line items",
                orderEvent.OrderId, orderEvent.LineItems.Count);

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process OrderCreated message");
            HandleFailure(ea);
        }
    }

    private void HandleFailure(BasicDeliverEventArgs ea)
    {
        var retryCount = MessageRetryPolicy.GetRetryCount(ea.BasicProperties.Headers);

        if (MessageRetryPolicy.ShouldDeadLetter(retryCount))
        {
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            _logger.LogWarning(
                "Message sent to DLQ after {RetryCount} failed attempts (x-retry-count={RetryCount})",
                retryCount + 1, retryCount);
            return;
        }

        // Ack then republish with incremented x-retry-count (requeue does not update headers).
        _channel!.BasicAck(ea.DeliveryTag, multiple: false);

        var nextRetryCount = MessageRetryPolicy.NextRetryCount(retryCount);
        var props = _channel.CreateBasicProperties();
        props.Persistent = ea.BasicProperties.Persistent;
        props.ContentType = ea.BasicProperties.ContentType;
        props.Headers = CopyHeaders(ea.BasicProperties.Headers);
        props.Headers["x-retry-count"] = nextRetryCount;

        _channel.BasicPublish(
            ea.Exchange,
            ea.RoutingKey,
            props,
            ea.Body.ToArray());

        _logger.LogWarning(
            "Message republished for retry {NextRetryCount}/{MaxRetryCount}",
            nextRetryCount, MessageRetryPolicy.MaxRetryCount);
    }

    private static Dictionary<string, object> CopyHeaders(IDictionary<string, object>? headers)
    {
        var copy = new Dictionary<string, object>();
        if (headers is null)
            return copy;

        foreach (var (key, value) in headers)
            copy[key] = value;

        return copy;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
