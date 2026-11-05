using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMove.Api.Models;
using TechMove.Api.Services;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/users")]
public sealed class UsersController(
    IAccountEmailSender emailSender,
    ILogger<UsersController> logger,
    UserManager<AppUser> userManager) : ControllerBase
{
    [HttpGet("{id}/profile-picture")]
    public async Task<IActionResult> GetProfilePicture(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user?.ProfilePicture is null || user.ProfilePictureContentType is null)
        {
            return NotFound();
        }

        return File(user.ProfilePicture, user.ProfilePictureContentType);
    }

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
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Account {UserId} was approved, but the approval email could not be sent.",
                    user.Id);
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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateManagedUserRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.UserName = request.Email.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return ValidationProblem(ModelState);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }
        var isManager = await userManager.IsInRoleAsync(user, AppRoles.Admin);
        if (user.Id == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "A manager cannot delete their own account",
                Detail = "Ask another manager to delete this account."
            });
        }
        if (isManager && (await userManager.GetUsersInRoleAsync(AppRoles.Admin)).Count <= 1)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The last manager cannot be deleted",
                Detail = "Promote another employee to manager before deleting this account."
            });
        }

        try
        {
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return Problem(title: "Employee deletion failed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Employee account is in use",
                Detail = "This employee owns logistics records and cannot be deleted until those records are reassigned."
            });
        }

        return NoContent();
    }
}