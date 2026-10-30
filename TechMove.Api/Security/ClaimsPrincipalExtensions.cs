using System.Security.Claims;
using TechMove.Contracts;

namespace TechMove.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static bool CanViewAllRecords(this ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.Admin) || user.IsInRole("ServiceClient");

    public static string? GetOwnerId(this ClaimsPrincipal user) =>
        user.IsInRole("ServiceClient")
            ? null
            : user.FindFirstValue(ClaimTypes.NameIdentifier);
}
