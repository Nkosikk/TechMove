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
