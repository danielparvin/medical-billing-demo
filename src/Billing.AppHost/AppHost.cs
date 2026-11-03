var builder = DistributedApplication.CreateBuilder(args);

// The SA password is an explicit parameter, read from Parameters:sql-password in this project's user secrets.
// SQL Server stores the password in the data volume the first time it starts and ignores later changes, so the
// value must never change silently: if the secret is missing, Aspire asks for it instead of generating a new one.
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// SQL Server in a container. The persistent lifetime and data volume keep the container and data between runs,
// so restarts are fast on stage.
var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

// Injected into the API as ConnectionStrings__Billing, the key Billing.Api already reads.
var billingDb = sql.AddDatabase("Billing");

// The health check makes WaitFor(api) below hold the UI back until the API has migrated, seeded and started,
// so nobody can reach the UI while its calls to the API would still time out.
var api = builder.AddProject<Projects.Billing_Api>("api")
    .WithReference(billingDb)
    .WaitFor(billingDb)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Billing_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
