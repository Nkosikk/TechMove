using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
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
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AdminSeed:DisplayName"] = "Integration Administrator",
                ["AdminSeed:Email"] = "admin@integration.test",
                ["AdminSeed:Password"] = "Admin123!"
            }));
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

    [Fact]
    public async Task Register_AssignsUserRole_AndReturnsToken()
    {
        using var client = CreateClient();
        var email = $"user-{Guid.NewGuid():N}@integration.test";

        var response = await RegisterAsync(client, email);

        Assert.Contains(AppRoles.User, response.Roles);
        Assert.DoesNotContain(AppRoles.Admin, response.Roles);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
    }

    [Fact]
    public async Task Clients_AreVisibleOnlyToTheirOwner()
    {
        using var firstUser = CreateClient();
        using var secondUser = CreateClient();
        await RegisterAndAuthenticateAsync(firstUser, $"first-{Guid.NewGuid():N}@integration.test");
        await RegisterAndAuthenticateAsync(secondUser, $"second-{Guid.NewGuid():N}@integration.test");
        var firstName = $"First Client {Guid.NewGuid():N}";
        var secondName = $"Second Client {Guid.NewGuid():N}";
        await firstUser.PostAsJsonAsync("/api/clients", new ClientDto
        {
            Name = firstName,
            ContactDetails = "first@example.test",
            Region = "Africa"
        });
        await secondUser.PostAsJsonAsync("/api/clients", new ClientDto
        {
            Name = secondName,
            ContactDetails = "second@example.test",
            Region = "Europe"
        });

        var visible = await firstUser.GetFromJsonAsync<List<ClientDto>>("/api/clients");

        Assert.Contains(visible!, item => item.Name == firstName);
        Assert.DoesNotContain(visible!, item => item.Name == secondName);
    }

    [Fact]
    public async Task ApproveContract_AsNormalUser_ReturnsForbidden()
    {
        using var client = CreateClient();
        await RegisterAndAuthenticateAsync(client, $"approver-{Guid.NewGuid():N}@integration.test");

        var response = await client.PostAsJsonAsync("/api/contracts/999/approve", new ApprovalRequest(null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private static async Task<LoginResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            DisplayName = "Integration User",
            Email = email,
            Password = "User123!",
            ConfirmPassword = "User123!"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private static async Task RegisterAndAuthenticateAsync(HttpClient client, string email)
    {
        var login = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });
}
