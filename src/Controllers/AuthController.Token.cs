using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DotNetNuxt.Abstractions.Options;
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
