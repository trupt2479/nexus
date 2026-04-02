using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexus.Contracts;
using Nexus.Orchestrator.Data;
using Nexus.Orchestrator.Models;

namespace Nexus.Orchestrator;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SagaOrchestrator.SagaOrchestratorClient _grpcClient;

    public Worker(
        ILogger<Worker> logger, 
        IServiceScopeFactory scopeFactory, 
        SagaOrchestrator.SagaOrchestratorClient grpcClient)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _grpcClient = grpcClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Orchestrating new Saga Transaction at: {time}", DateTimeOffset.Now);
            await ProcessSagaAsync(stoppingToken);

            // Wait 15 Seconds before spinning up the next Saga Pipeline
            await Task.Delay(15000, stoppingToken);
        }
    }

    private async Task ProcessSagaAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SagaDbContext>();

        var transaction = new SagaTransaction
        {
            Id = Guid.NewGuid(),
            CurrentState = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.SagaTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        string[] steps = { "ChargeAccount", "ProvisionVM", "SendWelcomeEmail" };
        var completedSteps = new Stack<SagaStep>();
        bool sagaFailed = false;

        for (int i = 0; i < steps.Length; i++)
        {
            var stepName = steps[i];
            
            var stepTrace = new SagaStep
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                StepName = stepName,
                Status = "Started",
                ExecutionOrder = i + 1
            };
            
            db.SagaSteps.Add(stepTrace);
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var response = await _grpcClient.ExecuteStepAsync(new SagaStepRequest
                {
                    TransactionId = transaction.Id.ToString(),
                    StepName = stepName,
                    Payload = "{}"
                }, cancellationToken: cancellationToken);

                if (response.IsSuccess)
                {
                    stepTrace.Status = "Completed";
                    await db.SaveChangesAsync(cancellationToken);
                    completedSteps.Push(stepTrace);
                }
                else
                {
                    _logger.LogWarning("Node denied step {StepName}. Triggering Saga Failure pathing...", stepName);
                    stepTrace.Status = "Failed";
                    stepTrace.Payload = response.ErrorMessage;
                    await db.SaveChangesAsync(cancellationToken);
                    
                    sagaFailed = true;
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical Exception generating gRPC Call for step {StepName}", stepName);
                stepTrace.Status = "Failed";
                stepTrace.Payload = ex.Message;
                await db.SaveChangesAsync(cancellationToken);
                
                sagaFailed = true;
                break;
            }
        }

        if (sagaFailed)
        {
            _logger.LogWarning("Saga {TransactionId} Faulted. Executing back-chain compensation...", transaction.Id);
            transaction.CurrentState = "Faulted";
            transaction.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            while (completedSteps.Count > 0)
            {
                var stepToCompensate = completedSteps.Pop();
                
                try
                {
                    await _grpcClient.CompensateStepAsync(new SagaStepRequest
                    {
                        TransactionId = transaction.Id.ToString(),
                        StepName = stepToCompensate.StepName,
                        Payload = "{}"
                    }, cancellationToken: cancellationToken);

                    stepToCompensate.Status = "Compensated";
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CRITICAL FAULT: Failed to compensate step {StepName} for {TransactionId}", stepToCompensate.StepName, transaction.Id);
                }
            }
            
            transaction.CurrentState = "RolledBack";
            transaction.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogCritical("Saga {TransactionId} roll-back resolved.", transaction.Id);
        }
        else
        {
            _logger.LogInformation("Saga {TransactionId} Completed Successfully Across All Clusters.", transaction.Id);
            transaction.CurrentState = "Completed";
            transaction.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
