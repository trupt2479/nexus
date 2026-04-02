using System;

namespace Nexus.Contracts.Events;

public record SagaFailureEvent(Guid TransactionId, string FailedStepName, string ErrorMessage);
