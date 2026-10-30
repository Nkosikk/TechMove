using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechMove.Api.Data;
using TechMove.Contracts;

namespace TechMove.Tests;

public sealed class TechMoveApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TechMoveTests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TechMoveDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TechMoveDbContext>>();
            services.RemoveAll<TechMoveDbContext>();
            services.AddDbContext<TechMoveDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}

public sealed class ApiIntegrationTests(TechMoveApiFactory factory) : IClassFixture<TechMoveApiFactory>
{
    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/contracts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetContracts_WithAuthentication_ReturnsOkAndJsonCollection()
    {
        using var client = CreateClient();
        await AuthenticateAsync(client);

        var response = await client.GetAsync("/api/contracts?status=Active");
        var contracts = await response.Content.ReadFromJsonAsync<List<ContractDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(contracts);
    }

    [Fact]
    public async Task CreateClient_ThenGetClients_ReturnsCreatedClient()
    {
        using var client = CreateClient();
        await AuthenticateAsync(client);
        var newClient = new ClientDto
        {
            Name = $"Integration Client {Guid.NewGuid():N}",
            ContactDetails = "integration@example.test",
            Region = "Africa"
        };

        var createResponse = await client.PostAsJsonAsync("/api/clients", newClient);
        var clients = await client.GetFromJsonAsync<List<ClientDto>>("/api/clients");

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Contains(clients!, item => item.Name == newClient.Name);
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/token",
            new LoginRequest("techmove-web", "DevelopmentOnly-ChangeMe!"));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });
}
