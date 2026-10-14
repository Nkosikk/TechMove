using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TechMove.Data;
using TechMove.Models;
using TechMove.Services;

namespace TechMove.Controllers;

public class ContractsController(
    TechMoveDbContext dbContext,
    AgreementFileService agreementFileService) : Controller
{
    public async Task<IActionResult> Index(
        ContractStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Contracts
            .AsNoTracking()
            .Include(contract => contract.Client)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(contract => contract.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(contract => contract.EndDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(contract => contract.StartDate <= toDate.Value);
        }

        ViewBag.Status = status;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(await query
            .OrderByDescending(contract => contract.StartDate)
            .ToListAsync(cancellationToken));
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateClientsAsync(cancellationToken);
        return View(new ContractFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ContractFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateClientsAsync(cancellationToken, model.ClientId);
            return View(model);
        }

        string? storedFileName = null;
        try
        {
            storedFileName = await agreementFileService.SaveAsync(model.AgreementFile!, cancellationToken);
            var contract = new Contract
            {
                ClientId = model.ClientId,
                Title = model.Title,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Status = model.Status,
                ServiceLevel = model.ServiceLevel,
                AgreementStoredFileName = storedFileName,
                AgreementOriginalFileName = Path.GetFileName(model.AgreementFile!.FileName)
            };

            dbContext.Contracts.Add(contract);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(model.AgreementFile), exception.Message);
            await PopulateClientsAsync(cancellationToken, model.ClientId);
            return View(model);
        }
        catch
        {
            agreementFileService.Delete(storedFileName);
            throw;
        }

        TempData["Success"] = "Contract and signed agreement created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts.FindAsync([id], cancellationToken);
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
            HasAgreement = contract.AgreementStoredFileName is not null
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ContractFormViewModel model,
        CancellationToken cancellationToken)
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

        var contract = await dbContext.Contracts.FindAsync([id], cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        string? replacementFileName = null;
        var previousFileName = contract.AgreementStoredFileName;
        try
        {
            if (model.AgreementFile is not null)
            {
                replacementFileName = await agreementFileService.SaveAsync(model.AgreementFile, cancellationToken);
                contract.AgreementStoredFileName = replacementFileName;
                contract.AgreementOriginalFileName = Path.GetFileName(model.AgreementFile.FileName);
            }

            contract.ClientId = model.ClientId;
            contract.Title = model.Title;
            contract.StartDate = model.StartDate;
            contract.EndDate = model.EndDate;
            contract.Status = model.Status;
            contract.ServiceLevel = model.ServiceLevel;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(model.AgreementFile), exception.Message);
            await PopulateClientsAsync(cancellationToken, model.ClientId);
            return View(model);
        }
        catch
        {
            agreementFileService.Delete(replacementFileName);
            throw;
        }

        if (replacementFileName is not null)
        {
            agreementFileService.Delete(previousFileName);
        }

        TempData["Success"] = "Contract updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts
            .AsNoTracking()
            .SingleOrDefaultAsync(contract => contract.Id == id, cancellationToken);
        if (contract?.AgreementStoredFileName is null)
        {
            return NotFound();
        }

        try
        {
            var stream = agreementFileService.OpenRead(contract.AgreementStoredFileName);
            return File(stream, "application/pdf", contract.AgreementOriginalFileName ?? "agreement.pdf");
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task PopulateClientsAsync(CancellationToken cancellationToken, int? selectedId = null)
    {
        var clients = await dbContext.Clients
            .AsNoTracking()
            .OrderBy(client => client.Name)
            .ToListAsync(cancellationToken);
        ViewBag.Clients = new SelectList(clients, nameof(Client.Id), nameof(Client.Name), selectedId);
    }
}
