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
[Route("api/service-requests")]
public sealed class ServiceRequestsController(
    TechMoveDbContext dbContext,
    ContractWorkflowService workflowService,
    ICurrencyService currencyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestDto>>> GetAll(CancellationToken cancellationToken)
    {
        var query = dbContext.ServiceRequests.AsNoTracking().AsQueryable();
        if (!User.CanViewAllRecords())
        {
            var ownerId = User.GetOwnerId();
            query = query.Where(request => request.OwnerId == ownerId);
        }

        return Ok(await query
            .OrderByDescending(request => request.CreatedAtUtc)
            .Select(request => new ServiceRequestDto
            {
                Id = request.Id,
                ContractId = request.ContractId,
                ContractTitle = request.Contract!.Title,
                ClientName = request.Contract.Client!.Name,
                Description = request.Description,
                CostUsd = request.CostUsd,
                LocalCostZar = request.LocalCostZar,
                ExchangeRate = request.ExchangeRate,
                Status = request.Status,
                CreatedAtUtc = request.CreatedAtUtc,
                OwnerName = request.Owner != null ? request.Owner.DisplayName : "Legacy record",
                ApprovedByName = request.ApprovedBy != null ? request.ApprovedBy.DisplayName : null,
                ApprovedAtUtc = request.ApprovedAtUtc,
                ApprovalNote = request.ApprovalNote
            })
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ServiceRequestDto>> Create(
        CreateServiceRequestRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = User.GetOwnerId();
        var contractQuery = dbContext.Contracts.Where(contract => contract.Id == request.ContractId);
        if (!User.CanViewAllRecords())
        {
            contractQuery = contractQuery.Where(contract => contract.OwnerId == ownerId);
        }
        var contract = await contractQuery.SingleOrDefaultAsync(cancellationToken);
        if (contract is null)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.ContractId)] = ["Select a valid contract."]
            }));
        }
        if (!workflowService.CanCreateServiceRequest(contract))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.ContractId)] = ["Service requests can only be created for approved, active contracts."]
            }));
        }

        try
        {
            var rate = await currencyService.GetUsdToZarRateAsync(cancellationToken);
            var serviceRequest = new ServiceRequest
            {
                ContractId = request.ContractId,
                Description = request.Description,
                CostUsd = request.CostUsd,
                ExchangeRate = rate,
                LocalCostZar = currencyService.CalculateZar(request.CostUsd, rate),
                Status = ServiceRequestStatus.New,
                CreatedAtUtc = DateTime.UtcNow,
                OwnerId = ownerId
            };
            dbContext.ServiceRequests.Add(serviceRequest);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Created($"api/service-requests/{serviceRequest.Id}", new ServiceRequestDto
            {
                Id = serviceRequest.Id,
                ContractId = contract.Id,
                ContractTitle = contract.Title,
                ClientName = string.Empty,
                Description = serviceRequest.Description,
                CostUsd = serviceRequest.CostUsd,
                LocalCostZar = serviceRequest.LocalCostZar,
                ExchangeRate = serviceRequest.ExchangeRate,
                Status = serviceRequest.Status,
                CreatedAtUtc = serviceRequest.CreatedAtUtc,
                OwnerName = User.FindFirst("display_name")?.Value ?? User.Identity?.Name ?? string.Empty
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            return Problem(
                title: "Currency service unavailable",
                detail: "The current exchange rate could not be retrieved. Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        UpdateServiceRequestStatusRequest request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = await dbContext.ServiceRequests.FindAsync([id], cancellationToken);
        if (serviceRequest is null)
        {
            return NotFound();
        }
        serviceRequest.Status = request.Status;
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
        var serviceRequest = await dbContext.ServiceRequests.FindAsync([id], cancellationToken);
        if (serviceRequest is null)
        {
            return NotFound();
        }

        serviceRequest.Status = ServiceRequestStatus.InProgress;
        serviceRequest.ApprovedById = User.GetOwnerId();
        serviceRequest.ApprovedAtUtc = DateTime.UtcNow;
        serviceRequest.ApprovalNote = request.Note?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
