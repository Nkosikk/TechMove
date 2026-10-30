using Microsoft.AspNetCore.Mvc;
using TechMove.Contracts;
using TechMove.Services;

namespace TechMove.Controllers;

public sealed class ClientsController(TechMoveApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await apiClient.GetClientsAsync(cancellationToken));

    public IActionResult Create() => View(new ClientDto());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClientDto client, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(client);
        }

        await apiClient.CreateClientAsync(client, cancellationToken);
        TempData["Success"] = "Client created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var client = await apiClient.GetClientAsync(id, cancellationToken);
        return client is null ? NotFound() : View(client);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ClientDto client, CancellationToken cancellationToken)
    {
        if (id != client.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return View(client);
        }

        await apiClient.UpdateClientAsync(id, client, cancellationToken);
        TempData["Success"] = "Client updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}