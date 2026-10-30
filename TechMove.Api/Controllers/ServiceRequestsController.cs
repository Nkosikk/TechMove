using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMove.Api.Data;
using TechMove.Api.Models;
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
    public async Task<ActionResult<IReadOnlyList<ServiceRequestDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await dbContext.ServiceRequests.AsNoTracking()
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
                CreatedAtUtc = request.CreatedAtUtc
            })
            .ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ServiceRequestDto>> Create(
        CreateServiceRequestRequest request,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts
            .SingleOrDefaultAsync(contract => contract.Id == request.ContractId, cancellationToken);
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
                [nameof(request.ContractId)] = ["Service requests cannot be created for Expired or On Hold contracts."]
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
                CreatedAtUtc = DateTime.UtcNow
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
                CreatedAtUtc = serviceRequest.CreatedAtUtc
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
}
