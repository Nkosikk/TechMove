using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Models;
using TechMove.Api.Services;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(
    ProfilePictureValidator pictureValidator,
    UserManager<AppUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> Get()
    {
        var user = await GetCurrentUserAsync();
        return user is null
            ? NotFound()
            : Ok(new UserProfileDto(user.DisplayName, user.Email!, user.ProfilePicture is not null));
    }

    [HttpGet("picture")]
    public async Task<IActionResult> GetPicture()
    {
        var user = await GetCurrentUserAsync();
        if (user?.ProfilePicture is null || user.ProfilePictureContentType is null)
        {
            return NotFound();
        }

        return File(user.ProfilePicture, user.ProfilePictureContentType);
    }

    [HttpPut("picture")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdatePicture(
        IFormFile profilePicture,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return NotFound();
        }

        try
        {
            var picture = await pictureValidator.ReadAndValidateAsync(profilePicture, cancellationToken);
            user.ProfilePicture = picture.Content;
            user.ProfilePictureContentType = picture.ContentType;
            var result = await userManager.UpdateAsync(user);
            return result.Succeeded
                ? NoContent()
                : Problem(title: "Profile picture update failed", statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid profile picture",
                Detail = exception.Message
            });
        }
    }

    [HttpDelete("picture")]
    public async Task<IActionResult> DeletePicture()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return NotFound();
        }

        user.ProfilePicture = null;
        user.ProfilePictureContentType = null;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? NoContent()
            : Problem(title: "Profile picture removal failed", statusCode: StatusCodes.Status500InternalServerError);
    }

    private Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? Task.FromResult<AppUser?>(null) : userManager.FindByIdAsync(userId);
    }
}
