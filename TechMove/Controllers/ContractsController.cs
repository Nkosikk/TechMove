using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TechMove.Contracts;
using TechMove.Models;
using TechMove.Services;

namespace TechMove.Controllers;

public sealed class ContractsController(TechMoveApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(
        ContractStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        ViewBag.Status = status;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(await apiClient.GetContractsAsync(status, fromDate, toDate, cancellationToken));
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateClientsAsync(cancellationToken);
        return View(new ContractFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContractFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateClientsAsync(cancellationToken, model.ClientId);
            return View(model);
        }

        try
        {
            await apiClient.CreateContractAsync(ToRequest(model), model.AgreementFile!, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "The contract could not be saved by the API. Check the agreement and try again.");
            await PopulateClientsAsync(cancellationToken, model.ClientId);
            return View(model);
        }

        TempData["Success"] = "Contract and signed agreement created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var contract = await apiClient.GetContractAsync(id, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        await PopulateClientsAsync(cancellationToken, contract.ClientId);
        return View(new ContractFormViewModel
        {
            Id = contract.Id,
            ClientId = contract.ClientId,
            Title = contract.Title,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            Status = contract.Status,
            ServiceLevel = contract.ServiceLevel,
            HasAgreement = contract.HasAgreement
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContractFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            await PopulateClientsAsync(cancellationToken, model.ClientId);
            return View(model);
        }

        try
        {
            await apiClient.UpdateContractAsync(id, ToRequest(model), model.AgreementFile, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "The contract could not be updated by the API. Check the agreement and try again.");
            await PopulateClientsAsync(cancellationToken, model.ClientId);
            return View(model);
        }

        TempData["Success"] = "Contract updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken cancellationToken)
    {
        var agreement = await apiClient.DownloadAgreementAsync(id, cancellationToken);
        return agreement is null
            ? NotFound()
            : File(agreement.Content, agreement.ContentType, agreement.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Approve(int id, string? note, CancellationToken cancellationToken)
    {
        await apiClient.ApproveContractAsync(id, note, cancellationToken);
        TempData["Success"] = "Contract approved and activated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateClientsAsync(CancellationToken cancellationToken, int? selectedId = null)
    {
        var clients = await apiClient.GetClientsAsync(cancellationToken);
        ViewBag.Clients = new SelectList(clients, nameof(ClientDto.Id), nameof(ClientDto.Name), selectedId);
    }

    private static ContractUpsertRequest ToRequest(ContractFormViewModel model) => new()
    {
        ClientId = model.ClientId,
        Title = model.Title,
        StartDate = model.StartDate,
        EndDate = model.EndDate,
        Status = model.Status,
        ServiceLevel = model.ServiceLevel
    };
}