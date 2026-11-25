using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DotNetNuxt.Abstractions.Options;
using NuxtIdentity.Core.Models;
using NuxtIdentity.AspNetCore.Models;

namespace YoFi.V3.Controllers;

public partial class AuthController
{
    /// <summary>Exchanges form-encoded credentials or a refresh token for tokens in Scalar.</summary>
    /// <param name="request">The token request containing credentials or refresh token.</param>
    /// <param name="startupOptions">Startup options for the application.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>An OAuth-style access token or error response.</returns>
    [AllowAnonymous]
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ScalarTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScalarTokenError), StatusCodes.Status400BadRequest)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Token(
        [FromForm] ScalarTokenRequest request,
        [FromServices] IOptions<StartupOptions> startupOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!startupOptions.Value.EnableSwaggerUi)
        {
            LogTokenRequestRejected("not_enabled");
            return NotFound();
        }

        return await base.ExchangeScalarToken(request, cancellationToken);
    }
}

/// <summary>An OAuth-style access token response for the API reference.</summary>
/// <param name="AccessToken">JWT used to authorize API requests.</param>
/// <param name="TokenType">Authorization header scheme.</param>
/// <param name="RefreshToken">Refresh token replaced on every successful refresh.</param>
public sealed record ScalarTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("refresh_token")] string RefreshToken);

/// <summary>An OAuth-style token request error.</summary>
/// <param name="Error">OAuth error code.</param>
/// <param name="ErrorDescription">Non-sensitive explanation of the error.</param>
public sealed record ScalarTokenError(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);
