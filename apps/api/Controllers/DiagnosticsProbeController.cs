using BankOps.Api.FeatureFlags;
using Microsoft.AspNetCore.Mvc;

namespace BankOps.Api.Controllers;

// Temporary — exists only to prove the error-format and feature-flag plumbing works end-to-end.
// Delete once real modules exercise these paths for real reasons.
[ApiController]
[Route("api/v1/diagprobe")]
public class DiagnosticsProbeController : ControllerBase
{
    [HttpGet("boom")]
    public IActionResult Boom() => throw new InvalidOperationException("Deliberate test failure.");

    [HttpGet("gated-demo")]
    [FeatureGate("DemoFeature")]
    public IActionResult GatedDemo() => Ok(new { message = "the flag was on" });
}
