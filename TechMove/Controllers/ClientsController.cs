using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechMove.Data;
using TechMove.Models;

namespace TechMove.Controllers;

public class ClientsController(TechMoveDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var clients = await dbContext.Clients
            .AsNoTracking()
            .Include(client => client.Contracts)
            .OrderBy(client => client.Name)
            .ToListAsync(cancellationToken);
        return View(clients);
    }

    public IActionResult Create() => View(new Client());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Client client, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(client);
        }

        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Client created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients.FindAsync([id], cancellationToken);
        return client is null ? NotFound() : View(client);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Client client, CancellationToken cancellationToken)
    {
        if (id != client.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(client);
        }

        var existing = await dbContext.Clients.FindAsync([id], cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = client.Name;
        existing.ContactDetails = client.ContactDetails;
        existing.Region = client.Region;
        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Client updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
