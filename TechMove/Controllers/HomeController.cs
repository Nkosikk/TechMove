using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net;
using System.Diagnostics;
using TechMove.Contracts;
using TechMove.Models;
using TechMove.Services;

namespace TechMove.Controllers
{
    public class HomeController(TechMoveApiClient apiClient) : Controller
    {
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            return View(await apiClient.GetDashboardAsync(cancellationToken));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> ApproveUser(string id, CancellationToken cancellationToken)
        {
            try
            {
                await apiClient.ApproveUserAsync(id, cancellationToken);
                TempData["Success"] = "Employee account approved. The employee can now sign in.";
            }
            catch (TechMoveApiException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["Error"] = "Your session expired. Sign in again before approving the account.";
                return RedirectToAction("Login", "Account");
            }
            catch (TechMoveApiException exception)
            {
                TempData["Error"] = exception.StatusCode switch
                {
                    HttpStatusCode.BadRequest => exception.Message,
                    HttpStatusCode.NotFound => "The employee account no longer exists or is no longer awaiting approval.",
                    HttpStatusCode.Forbidden => "You no longer have permission to approve employee accounts.",
                    _ => "The employee account could not be approved. Check the API logs and try again."
                };
            }
            catch (HttpRequestException)
            {
                TempData["Error"] = "The TechMove API could not be reached. Confirm that the API is running, then try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> UpdateUser(
            string id,
            UpdateManagedUserRequest model,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Enter a valid employee or manager name and email address.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await apiClient.UpdateUserAsync(id, model, cancellationToken);
                TempData["Success"] = "Account details updated.";
            }
            catch (HttpRequestException)
            {
                TempData["Error"] = "The account could not be updated. The email address may already be in use.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> DeleteUser(string id, CancellationToken cancellationToken)
        {
            try
            {
                await apiClient.DeleteUserAsync(id, cancellationToken);
                TempData["Success"] = "Employee account deleted.";
            }
            catch (HttpRequestException)
            {
                TempData["Error"] = "The account could not be deleted. Managers cannot delete themselves or the last manager, and owned logistics records must be reassigned first.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> PromoteUser(string id, CancellationToken cancellationToken)
        {
            await apiClient.PromoteUserAsync(id, cancellationToken);
            TempData["Success"] = "Employee promoted to manager. Their new role applies the next time they sign in.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
