using System;
using System.Net.Http;
using Polly;
using Polly.Retry;
using Microsoft.EntityFrameworkCore;
using Nexus.Orchestrator;
using Nexus.Orchestrator.Data;
using Nexus.Orchestrator.Infrastructure;
using Nexus.Contracts;

var builder = Host.CreateApplicationBuilder(args);

// Define Async Polly Retry Policy
var retryPolicy = Policy<HttpResponseMessage>
    .Handle<Exception>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

builder.Services.AddSingleton(retryPolicy);

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<RabbitMqListener>(); // Register RabbitMQ Custom Listener

builder.Services.AddDbContext<SagaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SagaDatabase")));

// Bind the gRPC Node Client + Resilience Wrapper
builder.Services.AddGrpcClient<SagaOrchestrator.SagaOrchestratorClient>(o => 
{
    var nodeServiceUrl = builder.Configuration["NodeServiceUrl"] ?? "https://localhost:5001";
    o.Address = new Uri(nodeServiceUrl);
})
.AddPolicyHandler(retryPolicy);

var host = builder.Build();
host.Run();
