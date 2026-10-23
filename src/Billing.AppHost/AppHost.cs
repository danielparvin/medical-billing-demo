var builder = DistributedApplication.CreateBuilder(args);

// SQL Server in a container. Aspire generates the SA password and keeps it in this project's user secrets.
// The persistent lifetime and data volume keep the container and data between runs, so restarts are fast on stage.
var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

// Injected into the API as ConnectionStrings__Billing, the key Billing.Api already reads.
var billingDb = sql.AddDatabase("Billing");

var api = builder.AddProject<Projects.Billing_Api>("api")
    .WithReference(billingDb)
    .WaitFor(billingDb);

builder.AddProject<Projects.Billing_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
