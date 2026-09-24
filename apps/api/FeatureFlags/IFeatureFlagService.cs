namespace BankOps.Api.FeatureFlags;

// FR-004: "Feature flags may be scoped by environment and allowed role/cohort." Config-backed for
// now (D-04's real backend is still an open Phase 0 decision) — real flags eventually live in
// platform.app_settings via modules/settings, read through this same interface so call sites never
// change when the backing store does.
public interface IFeatureFlagService
{
    bool IsEnabled(string flagName, string environment, IEnumerable<string> userRoles);
}
