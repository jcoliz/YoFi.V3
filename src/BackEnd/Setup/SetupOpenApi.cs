using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace YoFi.V3.BackEnd.Startup;

public static class SetupOpenApi
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "YoFi.V3 Backend";
                document.Info.Description = "Application boundary between .NET backend and YoFi.V3 frontend.";
                document.Info.Version = "v1";
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Enter your JWT token (without 'Bearer' prefix)"
                    },
                    ["ScalarLogin"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,
                        Description = "Sign in with your YoFi username and password. Use Refresh to obtain a new access token.",
                        Flows = new OpenApiOAuthFlows
                        {
                            Password = new OpenApiOAuthFlow
                            {
                                TokenUrl = new Uri("/api/auth/token", UriKind.Relative),
                                RefreshUrl = new Uri("/api/auth/token", UriKind.Relative),
                                Scopes = new Dictionary<string, string>()
                            }
                        }
                    }
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (endpointMetadata.OfType<IAuthorizeData>().Any() &&
                    !endpointMetadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                    });
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("ScalarLogin", context.Document)] = []
                    });
                }

                if (context.Description.ActionDescriptor is ControllerActionDescriptor action &&
                    action.ControllerTypeInfo.AsType() == typeof(Controllers.SampleDataController) &&
                    action.MethodInfo.Name == nameof(Controllers.SampleDataController.DownloadOfx) &&
                    operation.Responses is { } responses &&
                    responses.TryGetValue("200", out var response) &&
                    response is not null &&
                    response.Content is { } content)
                {
                    var binaryContent = content.TryGetValue("application/x-ofx", out var ofxContent)
                        ? ofxContent
                        : content.TryGetValue("application/octet-stream", out var octetStreamContent)
                            ? octetStreamContent
                            : throw new InvalidOperationException("The OFX download response has no binary content schema.");
                    content.Clear();
                    binaryContent.Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "binary"
                    };
                    content["application/x-ofx"] = binaryContent;
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }

    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle("YoFi.V3 Backend")
            .AddPreferredSecuritySchemes("ScalarLogin")
            .EnablePersistentAuthentication());
        return app;
    }
}
