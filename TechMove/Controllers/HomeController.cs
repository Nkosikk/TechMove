using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
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
