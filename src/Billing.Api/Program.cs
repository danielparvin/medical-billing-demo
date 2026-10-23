using Billing.Api;
using Billing.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

// The connection string is read when the DbContext is first resolved, so test hosts can override it.
builder.Services.AddDbContext<BillingDbContext>((services, options) =>
    options.UseSqlServer(
        services.GetRequiredService<IConfiguration>().GetConnectionString("Billing")
        ?? throw new InvalidOperationException("ConnectionStrings:Billing is not configured.")));

builder.Services.AddScoped<IBillRepository, BillRepository>();
builder.Services.AddScoped<IFeeScheduleRepository, FeeScheduleRepository>();
builder.Services.AddScoped<ProcessBillService>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();
app.MapDefaultEndpoints();

if (app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await db.Database.MigrateAsync();
    await new FeeScheduleSeeder(db).SeedAsync();
}

app.Run();

public partial class Program;
