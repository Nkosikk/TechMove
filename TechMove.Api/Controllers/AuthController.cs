using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TechMove.Api.Models;
using TechMove.Api.Security;
using TechMove.Api.Services;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IOptions<JwtOptions> options,
    IOptions<EmailDeliveryOptions> emailOptions,
    JwtTokenService tokenService,
    IAccountEmailSender emailSender,
    UserManager<AppUser> userManager) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<RegistrationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegistrationResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = new AppUser
        {
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim(),
            UserName = request.Email.Trim(),
            EmailConfirmed = false,
            IsApproved = false,
            RegisteredAtUtc = DateTime.UtcNow
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

        var roleResult = await userManager.AddToRoleAsync(user, AppRoles.User);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return ValidationProblem(ModelState);
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/Account/ConfirmEmail",
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["token"] = token
            });
        try
        {
            await emailSender.SendRegistrationConfirmationAsync(
                user.Email!,
                user.DisplayName,
                confirmationUrl,
                cancellationToken);
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }

        return Created(string.Empty, new RegistrationResponse(
            user.Id,
            user.Email!,
            "Registration submitted. Check your email to confirm your address, then a manager will review your employee account."));
    }

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return InvalidConfirmation();
        }
        if (user.EmailConfirmed)
        {
            return NoContent();
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded ? NoContent() : InvalidConfirmation();
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

        if (!user.EmailConfirmed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Email confirmation required",
                Detail = "Confirm your email address before a manager can review your employee account."
            });
        }

        if (!user.IsApproved)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Account awaiting approval",
                Detail = "A manager must approve this employee account before it can sign in."
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

    private ActionResult InvalidConfirmation()
    {
        ModelState.AddModelError(string.Empty, "The email confirmation link is invalid or has expired.");
        return ValidationProblem(ModelState);
    }
}
