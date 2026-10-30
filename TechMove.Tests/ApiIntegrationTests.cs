using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechMove.Api.Data;
using TechMove.Api.Services;
using TechMove.Contracts;

namespace TechMove.Tests;

public sealed class TechMoveApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TechMoveTests-{Guid.NewGuid():N}";
    public TestAccountEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AdminSeed:DisplayName"] = "Integration Administrator",
                ["AdminSeed:Email"] = "admin@integration.test",
                ["AdminSeed:Password"] = "Admin123!",
                ["EmailDelivery:Host"] = "localhost",
                ["EmailDelivery:Port"] = "1025",
                ["EmailDelivery:EnableSsl"] = "false",
                ["EmailDelivery:FromAddress"] = "noreply@integration.test",
                ["EmailDelivery:FromName"] = "TechMove Tests",
                ["EmailDelivery:PublicAppBaseUrl"] = "https://techmove.integration.test"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TechMoveDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TechMoveDbContext>>();
            services.RemoveAll<TechMoveDbContext>();
            services.AddDbContext<TechMoveDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton(EmailSender);
            services.AddSingleton<IAccountEmailSender>(EmailSender);
        });
    }
}

public sealed record TestAccountEmail(string Type, string Recipient, string? ConfirmationUrl = null);

public sealed class TestAccountEmailSender : IAccountEmailSender
{
    public List<TestAccountEmail> Sent { get; } = [];

    public Task SendRegistrationConfirmationAsync(
        string email,
        string displayName,
        string confirmationUrl,
        CancellationToken cancellationToken = default)
    {
        Sent.Add(new TestAccountEmail("Confirmation", email, confirmationUrl));
        return Task.CompletedTask;
    }

    public Task SendAccountApprovedAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        Sent.Add(new TestAccountEmail("Approval", email));
        return Task.CompletedTask;
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
    public async Task Register_CreatesPendingAccount_AndBlocksLogin()
    {
        using var client = CreateClient();
        var email = $"user-{Guid.NewGuid():N}@integration.test";

        var registration = await RegisterAsync(client, email);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "User123!"));

        Assert.False(string.IsNullOrWhiteSpace(registration.UserId));
        Assert.Contains("confirm", registration.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(factory.EmailSender.Sent,
            message => message.Type == "Confirmation" && message.Recipient == email);
        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ApproveUser_BeforeEmailConfirmation_ReturnsBadRequest()
    {
        using var userClient = CreateClient();
        using var managerClient = CreateClient();
        var email = $"unconfirmed-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(userClient, email);
        await AuthenticateAdminAsync(managerClient);

        var response = await managerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(factory.EmailSender.Sent,
            message => message.Type == "Approval" && message.Recipient == email);
    }

    [Fact]
    public async Task ApproveUser_AsAdmin_AllowsLoginWithUserRole()
    {
        using var userClient = CreateClient();
        using var adminClient = CreateClient();
        var email = $"approved-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(userClient, email);
        await ConfirmEmailAsync(userClient, email);
        await AuthenticateAdminAsync(adminClient);

        var approvalResponse = await adminClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { });
        var loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "User123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.NoContent, approvalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains(AppRoles.User, login!.Roles);
        Assert.Contains(factory.EmailSender.Sent,
            message => message.Type == "Approval" && message.Recipient == email);
    }

    [Fact]
    public async Task PromoteUser_AsAdmin_GrantsAdminRoleOnNextLogin()
    {
        using var userClient = CreateClient();
        using var adminClient = CreateClient();
        var email = $"promoted-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(userClient, email);
        await ConfirmEmailAsync(userClient, email);
        await AuthenticateAdminAsync(adminClient);
        (await adminClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { })).EnsureSuccessStatusCode();

        var promotionResponse = await adminClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/promote",
            new { });
        var loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "User123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.NoContent, promotionResponse.StatusCode);
        Assert.Contains(AppRoles.Admin, login!.Roles);
    }

    [Fact]
    public async Task PromoteUser_AsNormalUser_ReturnsForbidden()
    {
        using var client = CreateClient();
        await RegisterAndAuthenticateAsync(client, $"normal-{Guid.NewGuid():N}@integration.test");

        var response = await client.PostAsJsonAsync("/api/users/another-user/promote", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private static async Task<RegistrationResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            DisplayName = "Integration User",
            Email = email,
            Password = "User123!",
            ConfirmPassword = "User123!"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RegistrationResponse>())!;
    }

    private async Task RegisterAndAuthenticateAsync(HttpClient client, string email)
    {
        var registration = await RegisterAsync(client, email);
        await ConfirmEmailAsync(client, email);
        using var adminClient = CreateClient();
        await AuthenticateAdminAsync(adminClient);
        var approval = await adminClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { });
        approval.EnsureSuccessStatusCode();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "User123!"));
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private async Task ConfirmEmailAsync(HttpClient client, string email)
    {
        var emailMessage = factory.EmailSender.Sent.Last(message =>
            message.Type == "Confirmation" && message.Recipient == email);
        var query = QueryHelpers.ParseQuery(new Uri(emailMessage.ConfirmationUrl!).Query);
        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new ConfirmEmailRequest(query["userId"].ToString(), query["token"].ToString()));
        response.EnsureSuccessStatusCode();
    }

    private static async Task AuthenticateAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@integration.test", "Admin123!"));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });
}
