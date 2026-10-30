using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TechMove.Contracts;

namespace TechMove.Services;

public sealed record AgreementDownload(byte[] Content, string FileName, string ContentType);

public sealed class TechMoveApiClient(
    HttpClient httpClient,
    IOptions<TechMoveApiOptions> options,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly TechMoveApiOptions _options = options.Value;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private LoginResponse? _token;

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
        var token = await GetTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && _token.ExpiresAtUtc > timeProvider.GetUtcNow().UtcDateTime.AddMinutes(1))
        {
            return _token.AccessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && _token.ExpiresAtUtc > timeProvider.GetUtcNow().UtcDateTime.AddMinutes(1))
            {
                return _token.AccessToken;
            }

            using var response = await httpClient.PostAsJsonAsync(
                "api/auth/token",
                new LoginRequest(_options.Username, _options.Password),
                JsonOptions,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            _token = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("The TechMove API did not return an access token.");
            return _token.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
