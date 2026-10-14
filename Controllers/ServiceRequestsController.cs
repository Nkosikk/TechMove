using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TechMove.Data;
using TechMove.Models;
using TechMove.Services;

namespace TechMove.Controllers;

public class ServiceRequestsController(
    TechMoveDbContext dbContext,
    ContractWorkflowService workflowService,
    ICurrencyService currencyService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var requests = await dbContext.ServiceRequests
            .AsNoTracking()
            .Include(request => request.Contract)
            .ThenInclude(contract => contract!.Client)
            .OrderByDescending(request => request.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return View(requests);
    }

    public async Task<IActionResult> Create(int? contractId, CancellationToken cancellationToken)
    {
        await PopulateContractsAsync(cancellationToken, contractId);
        return View(new ServiceRequestCreateViewModel { ContractId = contractId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ServiceRequestCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts
            .AsNoTracking()
            .SingleOrDefaultAsync(contract => contract.Id == model.ContractId, cancellationToken);

        if (contract is null)
        {
            ModelState.AddModelError(nameof(model.ContractId), "Select a valid contract.");
        }
        else if (!workflowService.CanCreateServiceRequest(contract))
        {
            ModelState.AddModelError(nameof(model.ContractId),
                "Service requests cannot be created for Expired or On Hold contracts.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateContractsAsync(cancellationToken, model.ContractId);
            return View(model);
        }

        try
        {
            var rate = await currencyService.GetUsdToZarRateAsync(cancellationToken);
            var serviceRequest = new ServiceRequest
            {
                ContractId = model.ContractId,
                Description = model.Description,
                CostUsd = model.CostUsd,
                ExchangeRate = rate,
                LocalCostZar = currencyService.CalculateZar(model.CostUsd, rate),
                Status = ServiceRequestStatus.New,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.ServiceRequests.Add(serviceRequest);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty,
                "The current exchange rate could not be retrieved. Please try again.");
            await PopulateContractsAsync(cancellationToken, model.ContractId);
            return View(model);
        }

        TempData["Success"] = "Service request created with its current ZAR cost.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        int id,
        ServiceRequestStatus status,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.ServiceRequests.FindAsync([id], cancellationToken);
        if (request is null)
        {
            return NotFound();
        }

        request.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Service request status updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateContractsAsync(CancellationToken cancellationToken, int? selectedId = null)
    {
        var contracts = await dbContext.Contracts
            .AsNoTracking()
            .Include(contract => contract.Client)
            .Where(contract => contract.Status != ContractStatus.Expired
                && contract.Status != ContractStatus.OnHold)
            .OrderBy(contract => contract.Title)
            .Select(contract => new
            {
                contract.Id,
                Name = contract.Title + " — " + contract.Client!.Name
            })
            .ToListAsync(cancellationToken);

        ViewBag.Contracts = new SelectList(contracts, "Id", "Name", selectedId);
    }
}
