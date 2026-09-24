using BankOps.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankOps.Api.Controllers;

// FR-002 / BankOps_04_Frontend_Design.md: "Navigation is generated from a reviewed module
// manifest and an authorized server-supplied capability list. Hiding a menu item is only
// presentation; API remains independently protected." The manifest below is static code for now,
// not DB-driven — modules/identity doesn't exist yet, and a real module-registration system is
// more than Phase 2 needs. Every route this manifest reveals is still independently authorized by
// its own controller; nothing here is a substitute for that.
[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MeController(ICurrentPrincipal currentPrincipal) : ControllerBase
{
    private static readonly ModuleManifestEntry[] Modules =
    [
        new("catalog", "Service catalog", "/catalog", RequiresRole: null),
        new("appearance", "Appearance", "/admin/appearance", RequiresRole: "admin"),
        new("audit", "Audit log", "/admin/audit", RequiresRole: "admin"),
    ];

    [HttpGet("capabilities")]
    public IActionResult Capabilities()
    {
        var roles = currentPrincipal.Roles;
        var visibleModules = Modules.Where(m => m.RequiresRole is null || roles.Contains(m.RequiresRole));

        return Ok(new
        {
            user = new { subject = currentPrincipal.Subject, roles },
            modules = visibleModules,
        });
    }

    private record ModuleManifestEntry(string Id, string Label, string Route, string? RequiresRole);
}
