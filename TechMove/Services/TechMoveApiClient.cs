using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using TechMove.Contracts;

namespace TechMove.Services;

public sealed record AgreementDownload(byte[] Content, string FileName, string ContentType);
public sealed record ProfilePictureDownload(byte[] Content, string ContentType);
public sealed class TechMoveApiException(HttpStatusCode statusCode, string message)
    : HttpRequestException(message, null, statusCode);

public sealed class TechMoveApiClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        SendAuthenticationAsync("api/auth/login", request, cancellationToken);

    public async Task<RegistrationResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/register",
            request,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RegistrationResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The TechMove API returned an empty registration response.");
    }

    public async Task ConfirmEmailAsync(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/confirm-email",
            request,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/forgot-password",
            request,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/reset-password",
            request,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<DashboardDto>("api/dashboard", cancellationToken);

    public Task<UserProfileDto> GetProfileAsync(CancellationToken cancellationToken) =>
        GetAsync<UserProfileDto>("api/profile", cancellationToken);

    public Task<ProfilePictureDownload?> GetProfilePictureAsync(CancellationToken cancellationToken) =>
        DownloadProfilePictureAsync("api/profile/picture", cancellationToken);

    public Task<ProfilePictureDownload?> GetManagedProfilePictureAsync(
        string id,
        CancellationToken cancellationToken) =>
        DownloadProfilePictureAsync(
            $"api/users/{Uri.EscapeDataString(id)}/profile-picture",
            cancellationToken);

    public async Task UpdateProfilePictureAsync(
        IFormFile profilePicture,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Put, "api/profile/picture", cancellationToken);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(profilePicture.OpenReadStream());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
            profilePicture.ContentType ?? "application/octet-stream");
        content.Add(fileContent, "profilePicture", Path.GetFileName(profilePicture.FileName));
        request.Content = content;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProfilePictureAsync(CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Delete, "api/profile/picture", new { }, cancellationToken);

    public async Task ApproveUserAsync(string id, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Post,
            $"api/users/{Uri.EscapeDataString(id)}/approve",
            cancellationToken);
        request.Content = JsonContent.Create(new { }, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        string? message = null;
        try
        {
            var problem = JsonSerializer.Deserialize<ProblemDetails>(responseBody, JsonOptions);
            message = problem?.Detail ?? problem?.Title;
        }
        catch (JsonException)
        {
        }

        throw new TechMoveApiException(
            response.StatusCode,
            message ?? $"The TechMove API returned {(int)response.StatusCode} ({response.ReasonPhrase}).");
    }

    public async Task PromoteUserAsync(string id, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Post, $"api/users/{Uri.EscapeDataString(id)}/promote", new { }, cancellationToken);

    public async Task UpdateUserAsync(
        string id,
        UpdateManagedUserRequest request,
        CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Put, $"api/users/{Uri.EscapeDataString(id)}", request, cancellationToken);

    public async Task DeleteUserAsync(string id, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Delete, $"api/users/{Uri.EscapeDataString(id)}", new { }, cancellationToken);

    public Task<IReadOnlyList<ClientDto>> GetClientsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ClientDto>>("api/clients", cancellationToken);

    public Task<ClientDto?> GetClientAsync(int id, CancellationToken cancellationToken) =>
        GetOrDefaultAsync<ClientDto>($"api/clients/{id}", cancellationToken);

    public async Task CreateClientAsync(ClientDto client, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Post, "api/clients", client, cancellationToken);

    public async Task UpdateClientAsync(int id, ClientDto client, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Put, $"api/clients/{id}", client, cancellationToken);

    public Task<IReadOnlyList<ContractDto>> GetContractsAsync(
        ContractStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (status.HasValue)
        {
            query.Add($"status={Uri.EscapeDataString(status.Value.ToString())}");
        }
        if (fromDate.HasValue)
        {
            query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        }
        if (toDate.HasValue)
        {
            query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        }
        var path = "api/contracts" + (query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}");
        return GetAsync<IReadOnlyList<ContractDto>>(path, cancellationToken);
    }

    public Task<ContractDto?> GetContractAsync(int id, CancellationToken cancellationToken) =>
        GetOrDefaultAsync<ContractDto>($"api/contracts/{id}", cancellationToken);

    public Task CreateContractAsync(
        ContractUpsertRequest request,
        IFormFile agreementFile,
        CancellationToken cancellationToken) =>
        SendContractAsync(HttpMethod.Post, "api/contracts", request, agreementFile, cancellationToken);

    public Task UpdateContractAsync(
        int id,
        ContractUpsertRequest request,
        IFormFile? agreementFile,
        CancellationToken cancellationToken) =>
        SendContractAsync(HttpMethod.Put, $"api/contracts/{id}", request, agreementFile, cancellationToken);

    public async Task ApproveContractAsync(int id, string? note, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Post, $"api/contracts/{id}/approve", new ApprovalRequest(note), cancellationToken);

    public async Task<AgreementDownload?> DownloadAgreementAsync(int id, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/contracts/{id}/agreement", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "agreement.pdf";
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
        return new AgreementDownload(content, fileName, contentType);
    }

    public Task<IReadOnlyList<ServiceRequestDto>> GetServiceRequestsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ServiceRequestDto>>("api/service-requests", cancellationToken);

    public async Task CreateServiceRequestAsync(
        CreateServiceRequestRequest request,
        CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Post, "api/service-requests", request, cancellationToken);

    public async Task UpdateServiceRequestStatusAsync(
        int id,
        ServiceRequestStatus status,
        CancellationToken cancellationToken) =>
        await SendJsonAsync(
            HttpMethod.Patch,
            $"api/service-requests/{id}/status",
            new UpdateServiceRequestStatusRequest(status),
            cancellationToken);

    public async Task ApproveServiceRequestAsync(int id, string? note, CancellationToken cancellationToken) =>
        await SendJsonAsync(HttpMethod.Post, $"api/service-requests/{id}/approve", new ApprovalRequest(note), cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The TechMove API returned an empty response.");
    }

    private async Task<T?> GetOrDefaultAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<ProfilePictureDownload?> DownloadProfilePictureAsync(
        string path,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return new ProfilePictureDownload(
            await response.Content.ReadAsByteArrayAsync(cancellationToken),
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    private async Task SendJsonAsync<T>(
        HttpMethod method,
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, path, cancellationToken);
        request.Content = JsonContent.Create(value, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task SendContractAsync(
        HttpMethod method,
        string path,
        ContractUpsertRequest requestModel,
        IFormFile? agreementFile,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, path, cancellationToken);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(requestModel.ClientId.ToString(CultureInfo.InvariantCulture)), nameof(requestModel.ClientId) },
            { new StringContent(requestModel.Title), nameof(requestModel.Title) },
            { new StringContent(requestModel.StartDate.ToString("O", CultureInfo.InvariantCulture)), nameof(requestModel.StartDate) },
            { new StringContent(requestModel.EndDate.ToString("O", CultureInfo.InvariantCulture)), nameof(requestModel.EndDate) },
            { new StringContent(requestModel.Status.ToString()), nameof(requestModel.Status) },
            { new StringContent(requestModel.ServiceLevel), nameof(requestModel.ServiceLevel) }
        };
        if (agreementFile is not null)
        {
            var fileContent = new StreamContent(agreementFile.OpenReadStream());
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(agreementFile.ContentType ?? "application/pdf");
            content.Add(fileContent, "agreementFile", Path.GetFileName(agreementFile.FileName));
        }
        request.Content = content;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync();
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetTokenAsync()
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP request is available.");
        return await context.GetTokenAsync("access_token")
            ?? throw new UnauthorizedAccessException("The current session does not contain an API access token.");
    }

    private async Task<LoginResponse> SendAuthenticationAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(path, value, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The TechMove API returned an empty authentication response.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
