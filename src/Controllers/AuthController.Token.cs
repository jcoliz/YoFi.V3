using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NuxtIdentity.Core.Models;

namespace YoFi.V3.Controllers;

public partial class AuthController
{
    /// <summary>Exchanges form-encoded credentials or a refresh token for tokens in Scalar.</summary>
    /// <param name="grantType">The supported OAuth grant type: password or refresh_token.</param>
    /// <param name="username">The existing account's username.</param>
    /// <param name="password">The account password.</param>
    /// <param name="refreshToken">The refresh token to rotate when using the refresh_token grant.</param>
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
        [FromForm(Name = "grant_type")] string? grantType,
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm(Name = "refresh_token")] string? refreshToken,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(grantType))
        {
            LogTokenRequestRejected("invalid_request");
            return BadRequest(new ScalarTokenError("invalid_request", "grant_type is required."));
        }

        if (grantType is not ("password" or "refresh_token"))
        {
            LogTokenRequestRejected("unsupported_grant_type");
            return BadRequest(new ScalarTokenError("unsupported_grant_type", "Only password and refresh_token grants are supported."));
        }

        IActionResult result;
        if (grantType == "refresh_token")
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                LogTokenRequestRejected("invalid_request");
                return BadRequest(new ScalarTokenError("invalid_request", "refresh_token is required."));
            }

            result = await RefreshTokens(new RefreshRequest { RefreshToken = refreshToken });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LogTokenRequestRejected("invalid_request");
                return BadRequest(new ScalarTokenError("invalid_request", "username and password are required."));
            }

            result = await Login(new LoginRequest { Username = username, Password = password });
        }

        var tokens = result switch
        {
            OkObjectResult { Value: LoginResponse login } => login.Token,
            OkObjectResult { Value: RefreshResponse refresh } => refresh.Token,
            _ => null
        };
        if (tokens is not null)
        {
            LogTokenOk();
            return Ok(new ScalarTokenResponse(tokens.AccessToken, "Bearer", tokens.RefreshToken));
        }

        if (result is ObjectResult { StatusCode: StatusCodes.Status401Unauthorized })
        {
            return BadRequest(new ScalarTokenError("invalid_grant", "Invalid credentials or refresh token."));
        }

        return result;
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
