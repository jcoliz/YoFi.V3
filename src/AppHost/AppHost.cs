var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.YoFi_V3_BackEnd>("backend")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.YoFi_V3_FrontEnd_Blazor>("frontend-blazor")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddNpmApp("frontend-nuxt", "../FrontEnd.Nuxt")
    .WithReference(apiService)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
