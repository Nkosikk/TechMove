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
    public bool FailApprovalDelivery { get; set; }

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
        if (FailApprovalDelivery)
        {
            throw new InvalidOperationException("Simulated approval email delivery failure.");
        }

        Sent.Add(new TestAccountEmail("Approval", email));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string email,
        string displayName,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        Sent.Add(new TestAccountEmail("PasswordReset", email, resetUrl));
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
    public async Task ForgotPassword_ForUnknownEmail_ReturnsAcceptedWithoutSendingEmail()
    {
        using var client = CreateClient();
        var email = $"unknown-{Guid.NewGuid():N}@integration.test";

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(factory.EmailSender.Sent,
            message => message.Type == "PasswordReset" && message.Recipient == email);
    }

    [Fact]
    public async Task ResetPassword_WithEmailedToken_ReplacesPassword()
    {
        using var employeeClient = CreateClient();
        using var managerClient = CreateClient();
        var email = $"reset-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(employeeClient, email);
        await ConfirmEmailAsync(employeeClient, email);
        await AuthenticateAdminAsync(managerClient);
        (await managerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { })).EnsureSuccessStatusCode();

        var requestResponse = await employeeClient.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        var resetEmail = factory.EmailSender.Sent.Last(message =>
            message.Type == "PasswordReset" && message.Recipient == email);
        var query = QueryHelpers.ParseQuery(new Uri(resetEmail.ConfirmationUrl!).Query);
        var resetResponse = await employeeClient.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = query["email"].ToString(),
                Token = query["token"].ToString(),
                Password = "NewPassword123!",
                ConfirmPassword = "NewPassword123!"
            });
        var oldPasswordLogin = await employeeClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "User123!"));
        var newPasswordLogin = await employeeClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "NewPassword123!"));

        Assert.Equal(HttpStatusCode.Accepted, requestResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_EmployeeCanManageOwn_AndManagerCanView()
    {
        using var employeeClient = CreateClient();
        using var managerClient = CreateClient();
        var email = $"picture-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(employeeClient, email);
        await ConfirmEmailAsync(employeeClient, email);
        await AuthenticateAdminAsync(managerClient);
        (await managerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { })).EnsureSuccessStatusCode();
        await AuthenticateUserAsync(employeeClient, email);

        using var uploadContent = CreatePictureContent(ValidPng, "avatar.png", "image/png");
        var uploadResponse = await employeeClient.PutAsync("/api/profile/picture", uploadContent);
        var ownPictureResponse = await employeeClient.GetAsync("/api/profile/picture");
        var employeeCrossAccountResponse = await employeeClient.GetAsync(
            $"/api/users/{registration.UserId}/profile-picture");
        var managerPictureResponse = await managerClient.GetAsync(
            $"/api/users/{registration.UserId}/profile-picture");
        var deleteResponse = await employeeClient.DeleteAsync("/api/profile/picture");
        var removedPictureResponse = await employeeClient.GetAsync("/api/profile/picture");

        Assert.Equal(HttpStatusCode.NoContent, uploadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownPictureResponse.StatusCode);
        Assert.Equal("image/png", ownPictureResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, employeeCrossAccountResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, managerPictureResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removedPictureResponse.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_WithInvalidContent_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var email = $"invalid-picture-{Guid.NewGuid():N}@integration.test";
        await RegisterAndAuthenticateAsync(client, email);
        using var content = CreatePictureContent(
            "This is not an image"u8.ToArray(),
            "fake.png",
            "image/png");

        var response = await client.PutAsync("/api/profile/picture", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public async Task ApproveUser_WhenEmailDeliveryFails_StillAllowsLogin()
    {
        using var userClient = CreateClient();
        using var adminClient = CreateClient();
        var email = $"approval-email-failure-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(userClient, email);
        await ConfirmEmailAsync(userClient, email);
        await AuthenticateAdminAsync(adminClient);
        factory.EmailSender.FailApprovalDelivery = true;

        try
        {
            var approvalResponse = await adminClient.PostAsJsonAsync(
                $"/api/users/{registration.UserId}/approve",
                new { });
            var loginResponse = await userClient.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(email, "User123!"));

            Assert.Equal(HttpStatusCode.NoContent, approvalResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        }
        finally
        {
            factory.EmailSender.FailApprovalDelivery = false;
        }
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
    public async Task DeleteUser_AsAdmin_RemovesEmployeeAccount()
    {
        using var employeeClient = CreateClient();
        using var managerClient = CreateClient();
        var email = $"deleted-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(employeeClient, email);
        await ConfirmEmailAsync(employeeClient, email);
        await AuthenticateAdminAsync(managerClient);
        (await managerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { })).EnsureSuccessStatusCode();

        var deleteResponse = await managerClient.DeleteAsync($"/api/users/{registration.UserId}");
        var loginResponse = await employeeClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "User123!"));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_AsAdmin_ChangesEmployeeDetails()
    {
        using var employeeClient = CreateClient();
        using var managerClient = CreateClient();
        var oldEmail = $"employee-old-{Guid.NewGuid():N}@integration.test";
        var newEmail = $"employee-new-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(employeeClient, oldEmail);
        await ConfirmEmailAsync(employeeClient, oldEmail);
        await AuthenticateAdminAsync(managerClient);
        (await managerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { })).EnsureSuccessStatusCode();

        var updateResponse = await managerClient.PutAsJsonAsync(
            $"/api/users/{registration.UserId}",
            new UpdateManagedUserRequest { DisplayName = "Updated Employee", Email = newEmail });
        var loginResponse = await employeeClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(newEmail, "User123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal("Updated Employee", login!.DisplayName);
    }

    [Fact]
    public async Task UpdateAndDeleteManager_AsAnotherManager_Succeeds()
    {
        using var employeeClient = CreateClient();
        using var managerClient = CreateClient();
        var oldEmail = $"manager-old-{Guid.NewGuid():N}@integration.test";
        var newEmail = $"manager-new-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(employeeClient, oldEmail);
        await ConfirmEmailAsync(employeeClient, oldEmail);
        await AuthenticateAdminAsync(managerClient);
        (await managerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { })).EnsureSuccessStatusCode();
        (await managerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/promote",
            new { })).EnsureSuccessStatusCode();

        var updateResponse = await managerClient.PutAsJsonAsync(
            $"/api/users/{registration.UserId}",
            new UpdateManagedUserRequest { DisplayName = "Updated Manager", Email = newEmail });
        var deleteResponse = await managerClient.DeleteAsync($"/api/users/{registration.UserId}");
        var loginResponse = await employeeClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(newEmail, "User123!"));

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WhenManagerDeletesSelf_ReturnsBadRequest()
    {
        using var promotedManagerClient = CreateClient();
        using var seedManagerClient = CreateClient();
        var email = $"self-delete-{Guid.NewGuid():N}@integration.test";
        var registration = await RegisterAsync(promotedManagerClient, email);
        await ConfirmEmailAsync(promotedManagerClient, email);
        await AuthenticateAdminAsync(seedManagerClient);
        (await seedManagerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/approve",
            new { })).EnsureSuccessStatusCode();
        (await seedManagerClient.PostAsJsonAsync(
            $"/api/users/{registration.UserId}/promote",
            new { })).EnsureSuccessStatusCode();
        await AuthenticateUserAsync(promotedManagerClient, email);

        var response = await promotedManagerClient.DeleteAsync($"/api/users/{registration.UserId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z5xkAAAAASUVORK5CYII=");

    private static MultipartFormDataContent CreatePictureContent(
        byte[] content,
        string fileName,
        string contentType)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "profilePicture", fileName);
        return multipart;
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

    private static async Task AuthenticateUserAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "User123!"));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
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
