using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
            await apiClient.ApproveUserAsync(id, cancellationToken);
            TempData["Success"] = "Employee account approved. An email notification was sent and the employee can now sign in.";
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
