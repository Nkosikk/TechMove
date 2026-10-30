using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMove.Api.Data;
using TechMove.Api.Models;
using TechMove.Api.Security;
using TechMove.Api.Services;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/contracts")]
public sealed class ContractsController(
    TechMoveDbContext dbContext,
    AgreementFileService agreementFileService) : ControllerBase
{
    private static readonly Expression<Func<Contract, ContractDto>> DtoProjection = contract => new ContractDto
    {
        Id = contract.Id,
        ClientId = contract.ClientId,
        ClientName = contract.Client!.Name,
        Title = contract.Title,
        StartDate = contract.StartDate,
        EndDate = contract.EndDate,
        Status = contract.Status,
        ServiceLevel = contract.ServiceLevel,
        HasAgreement = contract.AgreementStoredFileName != null,
        AgreementOriginalFileName = contract.AgreementOriginalFileName,
        OwnerName = contract.Owner != null ? contract.Owner.DisplayName : "Legacy record",
        ApprovedByName = contract.ApprovedBy != null ? contract.ApprovedBy.DisplayName : null,
        ApprovedAtUtc = contract.ApprovedAtUtc,
        ApprovalNote = contract.ApprovalNote
    };

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContractDto>>> GetAll(
        ContractStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Contracts.AsNoTracking().AsQueryable();
        if (!User.CanViewAllRecords())
        {
            var ownerId = User.GetOwnerId();
            query = query.Where(contract => contract.OwnerId == ownerId);
        }
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

        return Ok(await query.OrderByDescending(contract => contract.StartDate)
            .Select(DtoProjection)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContractDto>> Get(int id, CancellationToken cancellationToken)
    {
        var query = dbContext.Contracts.AsNoTracking().Where(contract => contract.Id == id);
        if (!User.CanViewAllRecords())
        {
            var ownerId = User.GetOwnerId();
            query = query.Where(contract => contract.OwnerId == ownerId);
        }
        var contract = await query
            .Select(DtoProjection)
            .SingleOrDefaultAsync(cancellationToken);
        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ContractDto>> Create(
        [FromForm] ContractUpsertRequest request,
        IFormFile agreementFile,
        CancellationToken cancellationToken)
    {
        var ownerId = User.GetOwnerId();
        var clientQuery = dbContext.Clients.Where(client => client.Id == request.ClientId);
        if (!User.CanViewAllRecords())
        {
            clientQuery = clientQuery.Where(client => client.OwnerId == ownerId);
        }
        if (!await clientQuery.AnyAsync(cancellationToken))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.ClientId)] = ["Select a valid client."]
            }));
        }

        string? storedFileName = null;
        try
        {
            storedFileName = await agreementFileService.SaveAsync(agreementFile, cancellationToken);
            var contract = new Contract
            {
                ClientId = request.ClientId,
                Title = request.Title,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = ContractStatus.Draft,
                ServiceLevel = request.ServiceLevel,
                AgreementStoredFileName = storedFileName,
                AgreementOriginalFileName = Path.GetFileName(agreementFile.FileName),
                OwnerId = ownerId
            };
            dbContext.Contracts.Add(contract);
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = contract.Id }, await GetDto(contract.Id, cancellationToken));
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid agreement", Detail = exception.Message });
        }
        catch
        {
            agreementFileService.Delete(storedFileName);
            throw;
        }
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] ContractUpsertRequest request,
        IFormFile? agreementFile,
        CancellationToken cancellationToken)
    {
        var ownerId = User.GetOwnerId();
        var contractQuery = dbContext.Contracts.Where(contract => contract.Id == id);
        if (!User.CanViewAllRecords())
        {
            contractQuery = contractQuery.Where(contract => contract.OwnerId == ownerId);
        }
        var contract = await contractQuery.SingleOrDefaultAsync(cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }
        var clientQuery = dbContext.Clients.Where(client => client.Id == request.ClientId);
        if (!User.CanViewAllRecords())
        {
            clientQuery = clientQuery.Where(client => client.OwnerId == ownerId);
        }
        if (!await clientQuery.AnyAsync(cancellationToken))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.ClientId)] = ["Select a valid client."]
            }));
        }

        string? replacementFileName = null;
        var previousFileName = contract.AgreementStoredFileName;
        try
        {
            if (agreementFile is not null)
            {
                replacementFileName = await agreementFileService.SaveAsync(agreementFile, cancellationToken);
                contract.AgreementStoredFileName = replacementFileName;
                contract.AgreementOriginalFileName = Path.GetFileName(agreementFile.FileName);
            }
            contract.ClientId = request.ClientId;
            contract.Title = request.Title;
            contract.StartDate = request.StartDate;
            contract.EndDate = request.EndDate;
            contract.Status = ContractStatus.Draft;
            contract.ApprovedById = null;
            contract.ApprovedAtUtc = null;
            contract.ApprovalNote = null;
            contract.ServiceLevel = request.ServiceLevel;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid agreement", Detail = exception.Message });
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
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        UpdateContractStatusRequest request,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts.FindAsync([id], cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }
        contract.Status = request.Status;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Approve(
        int id,
        ApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts.FindAsync([id], cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        contract.Status = ContractStatus.Active;
        contract.ApprovedById = User.GetOwnerId();
        contract.ApprovedAtUtc = DateTime.UtcNow;
        contract.ApprovalNote = request.Note?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/agreement")]
    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken cancellationToken)
    {
        var query = dbContext.Contracts.AsNoTracking().Where(contract => contract.Id == id);
        if (!User.CanViewAllRecords())
        {
            var ownerId = User.GetOwnerId();
            query = query.Where(contract => contract.OwnerId == ownerId);
        }
        var contract = await query.SingleOrDefaultAsync(cancellationToken);
        if (contract?.AgreementStoredFileName is null)
        {
            return NotFound();
        }

        try
        {
            return File(
                agreementFileService.OpenRead(contract.AgreementStoredFileName),
                "application/pdf",
                contract.AgreementOriginalFileName ?? "agreement.pdf");
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<ContractDto> GetDto(int id, CancellationToken cancellationToken) =>
        await dbContext.Contracts.AsNoTracking()
            .Where(contract => contract.Id == id)
            .Select(DtoProjection)
            .SingleAsync(cancellationToken);
}
