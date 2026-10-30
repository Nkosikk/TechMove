using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TechMove.Api.Data;
using TechMove.Api.Security;
using TechMove.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TechMoveDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TechMoveDb")));

var agreementStorageOptions = builder.Configuration
    .GetSection(AgreementStorageOptions.SectionName)
    .Get<AgreementStorageOptions>() ?? new AgreementStorageOptions();
builder.Services.AddSingleton(agreementStorageOptions);
builder.Services.AddSingleton<AgreementFileValidator>();
builder.Services.AddSingleton<AgreementFileService>();
builder.Services.AddSingleton<ContractWorkflowService>();
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>(client =>
    client.BaseAddress = new Uri(builder.Configuration["CurrencyApi:BaseUrl"]
        ?? throw new InvalidOperationException("CurrencyApi:BaseUrl is not configured.")));

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => Encoding.UTF8.GetByteCount(options.Key) >= 32,
        "Jwt:Key must contain at least 32 bytes.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ServiceUsername)
        && !string.IsNullOrWhiteSpace(options.ServicePassword),
        "JWT service credentials must be configured.")
    .ValidateOnStart();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT settings are not configured.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<JwtTokenService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapControllers();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await ApplyMigrationsWithRetryAsync(app.Services, app.Logger);
}

app.Run();

static async Task ApplyMigrationsWithRetryAsync(IServiceProvider services, ILogger logger)
{
    const int maximumAttempts = 10;
    for (var attempt = 1; attempt <= maximumAttempts; attempt++)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TechMoveDbContext>();
            await dbContext.Database.MigrateAsync();
            return;
        }
        catch (Exception exception) when (attempt < maximumAttempts)
        {
            logger.LogWarning(exception,
                "Database migration attempt {Attempt}/{MaximumAttempts} failed; retrying.",
                attempt,
                maximumAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

public partial class Program;