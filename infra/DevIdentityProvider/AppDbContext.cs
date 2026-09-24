using Microsoft.EntityFrameworkCore;

namespace BankOps.DevIdentityProvider;

// In-memory store for OpenIddict's own client/token bookkeeping. Dev-only — resets on every
// restart, which is fine: this issuer exists purely to give apps/api something real to validate
// tokens against before D-01's actual identity provider (Bank AD/Entra) is wired up.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
    }
}
