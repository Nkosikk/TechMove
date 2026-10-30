using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using TechMove.Contracts;

namespace TechMove.Services;

public sealed record AgreementDownload(byte[] Content, string FileName, string ContentType);

public sealed class TechMoveApiClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        SendAuthenticationAsync("api/auth/login", request, cancellationToken);

    public Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        SendAuthenticationAsync("api/auth/register", request, cancellationToken);

    public Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<DashboardDto>("api/dashboard", cancellationToken);

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
