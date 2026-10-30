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
            ModelState.AddModelError(string.Empty, "The email address or password is incorrect.");
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }
    }

    [AllowAnonymous]
    public IActionResult Register() => View(new RegisterRequest());

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
            await SignInAsync(response, model.Email, false);
            TempData["Success"] = "Welcome to TechMove. Your account is ready.";
            return RedirectToAction("Index", "Home");
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
