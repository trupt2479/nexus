using Nexus.NodeService.Services;

var builder = WebApplication.CreateBuilder(args);

// Register gRPC
builder.Services.AddGrpc();

var app = builder.Build();

// Map the microservice endpoints
app.MapGrpcService<SagaNodeService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
