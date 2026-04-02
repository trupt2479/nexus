using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Nexus.Contracts;
using Nexus.Contracts.Events;

namespace Nexus.NodeService.Services;

public class SagaNodeService : SagaOrchestrator.SagaOrchestratorBase
{
    private readonly ILogger<SagaNodeService> _logger;

    public SagaNodeService(ILogger<SagaNodeService> logger)
    {
        _logger = logger;
    }

    public override Task<SagaStepResponse> ExecuteStep(SagaStepRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Attempting execution of step {StepName} for Transaction {TransactionId}", request.StepName, request.TransactionId);

        if (request.StepName == "ProvisionVM")
        {
            // Inject Chaos: 20% failure rate
            if (Random.Shared.Next(1, 101) <= 20)
            {
                _logger.LogWarning("CHAOS EVENT: Simulated failure for step {StepName} in Transaction {TransactionId}", request.StepName, request.TransactionId);
                PublishFailureEvent(request.TransactionId, request.StepName, "Simulated hardware allocation failure from Chaos Monkey.");
                
                return Task.FromResult(new SagaStepResponse
                {
                    TransactionId = request.TransactionId,
                    IsSuccess = false,
                    ErrorMessage = "Simulated hardware allocation failure from Chaos Monkey."
                });
            }
        }

        return Task.FromResult(new SagaStepResponse
        {
            TransactionId = request.TransactionId,
            IsSuccess = true,
            ResultPayload = "Step Completed Normally"
        });
    }

    public override Task<SagaStepResponse> CompensateStep(SagaStepRequest request, ServerCallContext context)
    {
        _logger.LogWarning("EXECUTING ROLLBACK: Reversing step {StepName} for Transaction {TransactionId}", request.StepName, request.TransactionId);

        return Task.FromResult(new SagaStepResponse
        {
            TransactionId = request.TransactionId,
            IsSuccess = true,
            ResultPayload = "Step Rolled Back Successfully"
        });
    }

    private void PublishFailureEvent(string transactionIdStr, string stepName, string error)
    {
        try
        {
            var hostName = Environment.GetEnvironmentVariable("RabbitMQHost") ?? "localhost";
            var factory = new ConnectionFactory { HostName = hostName };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "saga_failures",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            if (Guid.TryParse(transactionIdStr, out var transactionId))
            {
                var failureEvent = new SagaFailureEvent(transactionId, stepName, error);
                string message = JsonSerializer.Serialize(failureEvent);
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "",
                                     routingKey: "saga_failures",
                                     basicProperties: null,
                                     body: body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish chaos event to RabbitMQ on localhost.");
        }
    }
}
