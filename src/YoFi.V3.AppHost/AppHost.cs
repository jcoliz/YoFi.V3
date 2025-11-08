var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.YoFi_V3_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.YoFi_V3_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
