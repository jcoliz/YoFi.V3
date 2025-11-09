var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.YoFi_V3_BackEnd>("backend")
    .WithHttpHealthCheck("/health");

builder.AddNpmApp("frontend-nuxt", "../FrontEnd.Nuxt")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
