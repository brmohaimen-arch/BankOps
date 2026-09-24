using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankOps.Api.Controllers;

// Temporary — exists only to prove the OIDC/RBAC wiring works end-to-end (Phase 1 exit evidence:
// "unauthorized access is denied in the API, not hidden in the UI"). Delete once modules/identity
// has real endpoints and a real object-level permission test suite.
[ApiController]
[Route("api/v1/authprobe")]
public class AuthProbeController : ControllerBase
{
    [HttpGet("whoami")]
    [Authorize]
    public IActionResult WhoAmI() =>
        Ok(new
        {
            name = User.Identity?.Name,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });

    [HttpGet("admin-only")]
    [Authorize(Roles = "admin")]
    public IActionResult AdminOnly() => Ok(new { message = "you are an admin" });
}
