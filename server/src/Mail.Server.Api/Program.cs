using DbUp;
using Mail.Server.Api.Endpoints;
using Mail.Server.Api.Json;
using Mail.Server.Api.Models;
using Mail.Server.Api.Storage;
using Npgsql;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

var connStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings__Postgres");

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(connStr).Build());
builder.Services.AddSingleton<IMailStore, PostgresMailStore>();

var app = builder.Build();

var migrateOnly = args.Contains("--migrate")
    || string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATION_ONLY"), "true", StringComparison.OrdinalIgnoreCase);

// DbUp migration
{
    var upgrader = DeployChanges.To
        .PostgresqlDatabase(connStr)
        .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
        .WithTransaction()
        .LogToConsole()
        .Build();
    var result = upgrader.PerformUpgrade();
    if (!result.Successful)
        throw new Exception("DB migration failed: " + result.Error);
}

if (migrateOnly)
{
    Console.WriteLine("Migration completed. Exiting (--migrate / RUN_MIGRATION_ONLY).");
    return;
}

app.MapGet("/health", () => Results.Ok(new HealthResponse { Status = "ok" }));

app.MapPlayerEndpoints();
app.MapAdminEndpoints();

app.Run();
