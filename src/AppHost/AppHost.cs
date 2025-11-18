var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.YoFi_V3_BackEnd>("backend")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("APPLICATION__ENVIRONMENT", "Local");

builder.AddNpmApp("frontend-nuxt", "../FrontEnd.Nuxt")
    .WithEnvironment("NUXT_PUBLIC_SOLUTION_VERSION", "1.2.3")
    .WithReference(apiService)
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
