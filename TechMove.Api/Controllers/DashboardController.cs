using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMove.Api.Data;
using TechMove.Api.Security;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(TechMoveDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        var clients = dbContext.Clients.AsNoTracking().AsQueryable();
        var contracts = dbContext.Contracts.AsNoTracking().AsQueryable();
        var serviceRequests = dbContext.ServiceRequests.AsNoTracking().AsQueryable();
        var canViewAll = User.CanViewAllRecords();
        var isAdmin = User.IsInRole(AppRoles.Admin);
        if (!canViewAll)
        {
            var ownerId = User.GetOwnerId();
            clients = clients.Where(client => client.OwnerId == ownerId);
            contracts = contracts.Where(contract => contract.OwnerId == ownerId);
            serviceRequests = serviceRequests.Where(request => request.OwnerId == ownerId);
        }

        var pendingContracts = await contracts
            .Where(contract => contract.Status == ContractStatus.Draft)
            .OrderBy(contract => contract.StartDate)
            .Take(6)
            .Select(contract => new ContractDto
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
                OwnerName = contract.Owner != null ? contract.Owner.DisplayName : "Legacy record"
            })
            .ToListAsync(cancellationToken);
        var pendingUsers = isAdmin
            ? await dbContext.Users.AsNoTracking()
                .Where(user => user.EmailConfirmed && !user.IsApproved)
                .OrderBy(user => user.RegisteredAtUtc)
                .Take(10)
                .Select(user => new PendingUserDto
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email ?? string.Empty,
                    RegisteredAtUtc = user.RegisteredAtUtc
                })
                .ToListAsync(cancellationToken)
            : [];
        var users = isAdmin
            ? await dbContext.Users.AsNoTracking()
                .Where(user => user.IsApproved)
                .OrderBy(user => user.DisplayName)
                .Select(user => new ManagedUserDto
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email ?? string.Empty,
                    IsAdmin = dbContext.UserRoles.Any(userRole =>
                        userRole.UserId == user.Id
                        && dbContext.Roles.Any(role => role.Id == userRole.RoleId && role.Name == AppRoles.Admin)),
                    RegisteredAtUtc = user.RegisteredAtUtc
                })
                .ToListAsync(cancellationToken)
            : [];
        var pendingRequests = await serviceRequests
            .Where(request => request.Status == ServiceRequestStatus.New)
            .OrderBy(request => request.CreatedAtUtc)
            .Take(6)
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
                OwnerName = request.Owner != null ? request.Owner.DisplayName : "Legacy record"
            })
            .ToListAsync(cancellationToken);

        return Ok(new DashboardDto
        {
            ClientCount = await clients.CountAsync(cancellationToken),
            ContractCount = await contracts.CountAsync(cancellationToken),
            PendingContractCount = await contracts.CountAsync(
                contract => contract.Status == ContractStatus.Draft,
                cancellationToken),
            ServiceRequestCount = await serviceRequests.CountAsync(cancellationToken),
            PendingServiceRequestCount = await serviceRequests.CountAsync(
                request => request.Status == ServiceRequestStatus.New,
                cancellationToken),
            UserCount = canViewAll ? await dbContext.Users.CountAsync(cancellationToken) : 0,
            PendingUserCount = isAdmin
                ? await dbContext.Users.CountAsync(
                    user => user.EmailConfirmed && !user.IsApproved,
                    cancellationToken)
                : 0,
            PendingContracts = pendingContracts,
            PendingServiceRequests = pendingRequests,
            PendingUsers = pendingUsers,
            Users = users
        });
    }
}
