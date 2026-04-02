using System;
using System.Collections.Generic;

namespace Nexus.Orchestrator.Models;

public class SagaTransaction
{
    public Guid Id { get; set; }
    public string CurrentState { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation Property
    public ICollection<SagaStep> Steps { get; set; } = new List<SagaStep>();
}
