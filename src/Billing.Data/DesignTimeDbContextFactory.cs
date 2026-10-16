using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Billing.Data;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer("Server=.;Database=BillingDesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new BillingDbContext(options);
    }
}
