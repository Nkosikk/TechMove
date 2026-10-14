using Microsoft.EntityFrameworkCore;
using TechMove.Data;
using TechMove.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
