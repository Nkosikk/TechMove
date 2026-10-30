using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Models;
using TechMove.Api.Services;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/users")]
public sealed class UsersController(
    IAccountEmailSender emailSender,
    UserManager<AppUser> userManager) : ControllerBase
{
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!user.IsApproved)
        {
            if (!user.EmailConfirmed)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Email is not confirmed",
                    Detail = "The employee must confirm their email address before a manager can approve the account."
                });
            }

            user.IsApproved = true;
            user.ApprovedAtUtc = DateTime.UtcNow;
            user.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Problem(title: "Account approval failed", statusCode: StatusCodes.Status500InternalServerError);
            }

            try
            {
                await emailSender.SendAccountApprovedAsync(
                    user.Email!,
                    user.DisplayName,
                    cancellationToken);
            }
            catch
            {
                user.IsApproved = false;
                user.ApprovedAtUtc = null;
                user.ApprovedById = null;
                await userManager.UpdateAsync(user);
                throw;
            }
        }

        return NoContent();
    }

    [HttpPost("{id}/promote")]
    public async Task<IActionResult> Promote(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }
        if (!user.IsApproved)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Account is not approved",
                Detail = "Approve the employee account before granting manager access."
            });
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var result = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!result.Succeeded)
            {
                return Problem(title: "Manager promotion failed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        return NoContent();
    }
}