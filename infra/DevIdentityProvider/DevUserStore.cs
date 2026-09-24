namespace BankOps.DevIdentityProvider;

// Dev-only human users for the interactive login screen. Real users come from D-01's actual IdP
// (Bank AD/Entra) — this exists only so apps/web has something real to authenticate a person
// against before that integration happens. Plaintext passwords are fine here specifically because
// this store is never reachable outside localhost and is rebuilt from scratch on every restart.
public record DevUser(string Email, string Password, string DisplayName, string Role);

public static class DevUserStore
{
    public static readonly IReadOnlyList<DevUser> Users =
    [
        new("admin@bankops.dev", "admin123", "Platform Admin (dev)", "admin"),
        new("viewer@bankops.dev", "viewer123", "Viewer (dev)", "viewer"),
    ];

    public static DevUser? Validate(string email, string password) =>
        Users.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);
}
