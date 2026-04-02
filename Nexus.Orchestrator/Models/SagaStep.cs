using System;

namespace Nexus.Orchestrator.Models;

public class SagaStep
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = "Started";
    public string Payload { get; set; } = string.Empty;
    public int ExecutionOrder { get; set; }
    
    // Navigation Property
    public SagaTransaction Transaction { get; set; } = null!;
}
