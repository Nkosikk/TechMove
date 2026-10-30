using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMove.Api.Data;
using TechMove.Api.Models;
using TechMove.Api.Security;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public sealed class ClientsController(TechMoveDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetAll(CancellationToken cancellationToken)
    {
        var query = dbContext.Clients.AsNoTracking().AsQueryable();
        if (!User.CanViewAllRecords())
        {
            var ownerId = User.GetOwnerId();
            query = query.Where(client => client.OwnerId == ownerId);
        }

        return Ok(await query
            .OrderBy(client => client.Name)
            .Select(client => new ClientDto
            {
                Id = client.Id,
                Name = client.Name,
                ContactDetails = client.ContactDetails,
                Region = client.Region,
                ContractCount = client.Contracts.Count,
                OwnerName = client.Owner != null ? client.Owner.DisplayName : "Legacy record"
            })
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> Get(int id, CancellationToken cancellationToken)
    {
        var query = dbContext.Clients.AsNoTracking().Where(client => client.Id == id);
        if (!User.CanViewAllRecords())
        {
            var ownerId = User.GetOwnerId();
            query = query.Where(client => client.OwnerId == ownerId);
        }

        var client = await query
            .Select(client => new ClientDto
            {
                Id = client.Id,
                Name = client.Name,
                ContactDetails = client.ContactDetails,
                Region = client.Region,
                ContractCount = client.Contracts.Count,
                OwnerName = client.Owner != null ? client.Owner.DisplayName : "Legacy record"
            })
            .SingleOrDefaultAsync(cancellationToken);
        return client is null ? NotFound() : Ok(client);
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(ClientDto request, CancellationToken cancellationToken)
    {
        var client = new Client
        {
            Name = request.Name,
            ContactDetails = request.ContactDetails,
            Region = request.Region,
            OwnerId = User.GetOwnerId()
        };
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);
        request.Id = client.Id;
        request.ContractCount = 0;
        request.OwnerName = User.FindFirst("display_name")?.Value ?? User.Identity?.Name ?? string.Empty;
        return CreatedAtAction(nameof(Get), new { id = client.Id }, request);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ClientDto request, CancellationToken cancellationToken)
    {
        var query = dbContext.Clients.Where(client => client.Id == id);
        if (!User.CanViewAllRecords())
        {
            var ownerId = User.GetOwnerId();
            query = query.Where(client => client.OwnerId == ownerId);
        }
        var client = await query.SingleOrDefaultAsync(cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        client.Name = request.Name;
        client.ContactDetails = request.ContactDetails;
        client.Region = request.Region;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
