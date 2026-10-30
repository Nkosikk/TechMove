using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TechMove.Api.Security;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IOptions<JwtOptions> options,
    JwtTokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> CreateToken(LoginRequest request)
    {
        var configured = options.Value;
        if (!FixedTimeEquals(request.Username, configured.ServiceUsername)
            || !FixedTimeEquals(request.Password, configured.ServicePassword))
        {
            return Unauthorized();
        }

        return Ok(tokenService.CreateToken(request.Username));
    }

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied ?? string.Empty);
        var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
        return suppliedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
