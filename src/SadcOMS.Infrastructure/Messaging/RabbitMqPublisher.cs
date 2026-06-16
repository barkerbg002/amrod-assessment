using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace SadcOMS.Infrastructure.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync(string routingKey, object message, CancellationToken ct = default);
}

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

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
        DeclareTopology();
    }

    private void DeclareTopology()
    {
        _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true);

        _channel.QueueDeclare(_options.OrderCreatedDlq, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(_options.OrderCreatedQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = _options.OrderCreatedDlq
            });
        _channel.QueueBind(_options.OrderCreatedQueue, _options.ExchangeName, _options.OrderCreatedRoutingKey);
    }

    public Task PublishAsync(string routingKey, object message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.MessageId = Guid.NewGuid().ToString();
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(_options.ExchangeName, routingKey, properties, body);
        _logger.LogInformation("Published message to {RoutingKey}", routingKey);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
