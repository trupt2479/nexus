using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Nexus.Contracts.Events;

namespace Nexus.Orchestrator.Infrastructure;

public class RabbitMqListener : BackgroundService
{
    private readonly ILogger<RabbitMqListener> _logger;
    private IConnection _connection = null!;
    private IModel _channel = null!;

    public RabbitMqListener(ILogger<RabbitMqListener> logger)
    {
        _logger = logger;
        InitRabbitMq();
    }

    private void InitRabbitMq()
    {
        try
        {
            var hostName = Environment.GetEnvironmentVariable("RabbitMQHost") ?? "localhost";
            var factory = new ConnectionFactory { HostName = hostName };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: "saga_failures",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ broker on localhost.");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null) return Task.CompletedTask;

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            
            try
            {
                var failureEvent = JsonSerializer.Deserialize<SagaFailureEvent>(message);
                if (failureEvent != null)
                {
                    _logger.LogWarning("Received SagaFailureEvent: Transaction {TransactionId} failed at {StepName} due to {Error}", 
                        failureEvent.TransactionId, failureEvent.FailedStepName, failureEvent.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize saga failure event.");
            }
        };

        _channel.BasicConsume(queue: "saga_failures",
                             autoAck: true,
                             consumer: consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
