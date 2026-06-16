namespace SadcOMS.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "sadcoms.events";
    public string OrderCreatedRoutingKey { get; set; } = "order.created";
    public string OrderCreatedQueue { get; set; } = "order.created.queue";
    public string OrderCreatedDlq { get; set; } = "order.created.dlq";
}
