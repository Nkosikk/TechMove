using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Contracts;
using TechMove.Models;
using TechMove.Services;

namespace TechMove.Controllers;

public sealed class AccountController(TechMoveApiClient apiClient) : Controller
{
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        try
        {
            var response = await apiClient.LoginAsync(
                new LoginRequest(model.Email, model.Password),
                cancellationToken);
            await SignInAsync(response, model.Email, model.RememberMe);
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home")!);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "Sign-in failed. Confirm your email first, then wait for a manager to approve your employee account.");
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }
    }

    [AllowAnonymous]
    public IActionResult Register() => View(new RegisterRequest());

    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordRequest());

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await apiClient.ForgotPasswordAsync(model, cancellationToken);
            TempData["Success"] = "If that email belongs to a confirmed TechMove account, a password reset link has been sent.";
            return RedirectToAction(nameof(Login));
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "The password reset request could not be submitted. Please try again.");
            return View(model);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "This password reset link is invalid or has expired.";
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordRequest { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await apiClient.ResetPasswordAsync(model, cancellationToken);
            TempData["Success"] = "Your password has been reset. You can now sign in with the new password.";
            return RedirectToAction(nameof(Login));
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "The reset link is invalid or expired, or the new password does not meet the requirements.");
            return View(model);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(
        string userId,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            await apiClient.ConfirmEmailAsync(
                new ConfirmEmailRequest(userId, token),
                cancellationToken);
            TempData["Success"] = "Email confirmed. A manager can now review your employee account. You will receive another email when it is approved.";
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "This email confirmation link is invalid or has expired.";
        }

        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var response = await apiClient.RegisterAsync(model, cancellationToken);
            TempData["Success"] = response.Message;
            return RedirectToAction(nameof(Login));
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "Registration could not be completed. The email may already be registered or the password does not meet the requirements.");
            return View(model);
        }
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken) =>
        View(new ProfileViewModel
        {
            Profile = await apiClient.GetProfileAsync(cancellationToken)
        });

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadProfilePicture(
        IFormFile? profilePicture,
        CancellationToken cancellationToken)
    {
        if (profilePicture is null || profilePicture.Length == 0)
        {
            TempData["Error"] = "Select a JPEG, PNG, or WebP picture to upload.";
            return RedirectToAction(nameof(Profile));
        }

        try
        {
            await apiClient.UpdateProfilePictureAsync(profilePicture, cancellationToken);
            TempData["Success"] = "Profile picture updated.";
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "The picture could not be uploaded. Use a valid JPEG, PNG, or WebP image no larger than 1 MB.";
        }
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProfilePicture(CancellationToken cancellationToken)
    {
        await apiClient.DeleteProfilePictureAsync(cancellationToken);
        TempData["Success"] = "Profile picture removed.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ProfilePicture(CancellationToken cancellationToken)
    {
        var picture = await apiClient.GetProfilePictureAsync(cancellationToken);
        return picture is null ? NotFound() : File(picture.Content, picture.ContentType);
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> ManagedProfilePicture(
        string id,
        CancellationToken cancellationToken)
    {
        var picture = await apiClient.GetManagedProfilePictureAsync(id, cancellationToken);
        return picture is null ? NotFound() : File(picture.Content, picture.ContentType);
    }

    public IActionResult AccessDenied() => View();

    private async Task SignInAsync(LoginResponse response, string email, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, response.DisplayName),
            new(ClaimTypes.Email, email),
            new("display_name", response.DisplayName)
        };
        claims.AddRange(response.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme));
        var properties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = new DateTimeOffset(response.ExpiresAtUtc, TimeSpan.Zero)
        };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = response.AccessToken }
        ]);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);
    }
}
