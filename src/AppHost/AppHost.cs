var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.YoFi_V3_BackEnd>("backend")
    .WithHttpHealthCheck("/health");

builder.AddNpmApp("frontend-nuxt", "../FrontEnd.Nuxt")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
