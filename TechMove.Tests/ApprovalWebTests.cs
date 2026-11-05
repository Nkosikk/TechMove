using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using TechMove.Controllers;
using TechMove.Services;

namespace TechMove.Tests;

public sealed class ApprovalWebTests
{
    [Fact]
    public async Task ApproveUser_WhenApiRejectsRequest_ShowsDashboardError()
    {
        const string errorMessage = "The employee account cannot be approved in its current state.";
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new ProblemDetails
            {
                Title = "Account approval failed",
                Detail = errorMessage
            })
        }))
        {
            BaseAddress = new Uri("https://techmove.test/")
        };
        var httpContext = CreateHttpContext();
        var apiClient = new TechMoveApiClient(
            httpClient,
            new HttpContextAccessor { HttpContext = httpContext });
        var controller = new HomeController(apiClient)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        var result = await controller.ApproveUser("employee-id", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(errorMessage, controller.TempData["Error"]);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "test-access-token" }
        ]);
        var authenticationService = new TestAuthenticationService(
            AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("Test")),
                properties,
                "Test")));
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authenticationService)
            .BuildServiceProvider();

        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class TestAuthenticationService(AuthenticateResult result) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(result);

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
