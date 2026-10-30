using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TechMove.Api.Models;
using TechMove.Api.Security;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IOptions<JwtOptions> options,
    JwtTokenService tokenService,
    UserManager<AppUser> userManager) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request)
    {
        var user = new AppUser
        {
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim(),
            UserName = request.Email.Trim(),
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return ValidationProblem(ModelState);
        }

        await userManager.AddToRoleAsync(user, AppRoles.User);
        var roles = await userManager.GetRolesAsync(user);
        return Created(string.Empty, tokenService.CreateToken(
            user.Id,
            user.Email!,
            user.DisplayName,
            roles.ToArray()));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Username.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Sign in failed",
                Detail = "The email address or password is incorrect."
            });
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(tokenService.CreateToken(user.Id, user.Email!, user.DisplayName, roles.ToArray()));
    }

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
