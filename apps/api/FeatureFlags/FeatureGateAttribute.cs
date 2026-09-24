using Microsoft.AspNetCore.Mvc.Filters;

namespace BankOps.Api.FeatureFlags;

// FR-004: "direct URL cannot bypass flag" — this runs as an action filter, server-side, before the
// action executes. It returns 404, not 403: a disabled feature's routes should look like they
// don't exist, not like a permission you're missing (that would leak the feature's existence to a
// client probing for it).
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FeatureGateAttribute(string flagName) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var flags = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
        var environment = context.HttpContext.RequestServices
            .GetRequiredService<IHostEnvironment>().EnvironmentName;
        var roles = context.HttpContext.User.Claims
            .Where(c => c.Type == "role")
            .Select(c => c.Value);

        if (!flags.IsEnabled(flagName, environment, roles))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.NotFoundResult();
            return;
        }

        await next();
    }
}
