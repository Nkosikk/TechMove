using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove.Contracts;
using TechMove.Services;

namespace TechMove.Controllers;

public sealed class ServiceRequestsController(TechMoveApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await apiClient.GetServiceRequestsAsync(cancellationToken));

    public async Task<IActionResult> Create(int? contractId, CancellationToken cancellationToken)
    {
        await PopulateContractsAsync(cancellationToken, contractId);
        return View(new CreateServiceRequestRequest { ContractId = contractId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateServiceRequestRequest model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateContractsAsync(cancellationToken, model.ContractId);
            return View(model);
        }

        try
        {
            await apiClient.CreateServiceRequestAsync(model, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty,
                "The service request could not be created. Verify the contract and try again.");
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
        await apiClient.UpdateServiceRequestStatusAsync(id, status, cancellationToken);
        TempData["Success"] = "Service request status updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateContractsAsync(CancellationToken cancellationToken, int? selectedId = null)
    {
        var contracts = await apiClient.GetContractsAsync(null, null, null, cancellationToken);
        var availableContracts = contracts
            .Where(contract => contract.Status is not ContractStatus.Expired and not ContractStatus.OnHold)
            .OrderBy(contract => contract.Title)
            .Select(contract => new
            {
                contract.Id,
                Name = $"{contract.Title} — {contract.ClientName}"
            });
        ViewBag.Contracts = new SelectList(availableContracts, "Id", "Name", selectedId);
    }
}