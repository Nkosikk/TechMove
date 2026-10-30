using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMove.Api.Data;
using TechMove.Api.Models;
using TechMove.Contracts;

namespace TechMove.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public sealed class ClientsController(TechMoveDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await dbContext.Clients.AsNoTracking()
            .OrderBy(client => client.Name)
            .Select(client => new ClientDto
            {
                Id = client.Id,
                Name = client.Name,
                ContactDetails = client.ContactDetails,
                Region = client.Region,
                ContractCount = client.Contracts.Count
            })
            .ToListAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> Get(int id, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients.AsNoTracking()
            .Where(client => client.Id == id)
            .Select(client => new ClientDto
            {
                Id = client.Id,
                Name = client.Name,
                ContactDetails = client.ContactDetails,
                Region = client.Region,
                ContractCount = client.Contracts.Count
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
            Region = request.Region
        };
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);
        request.Id = client.Id;
        request.ContractCount = 0;
        return CreatedAtAction(nameof(Get), new { id = client.Id }, request);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ClientDto request, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients.FindAsync([id], cancellationToken);
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
