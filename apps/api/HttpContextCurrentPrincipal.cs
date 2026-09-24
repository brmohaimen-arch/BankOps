using BankOps.Contracts;

namespace BankOps.Api;

public class HttpContextCurrentPrincipal(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipal
{
    private System.Security.Claims.ClaimsPrincipal User =>
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("No HttpContext available.");

    public string Subject =>
        User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("Token has no 'sub' claim.");

    public IReadOnlyCollection<string> Roles =>
        User.FindAll("role").Select(c => c.Value).ToArray();

    public bool IsInRole(string role) => User.IsInRole(role);
}
