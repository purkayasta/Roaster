using System.Reflection;
using DbUp;

var connectionString = args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING")
    ?? throw new InvalidOperationException("No connection string provided.");

EnsureDatabase.For.PostgresqlDatabase(connectionString);

var assembly = Assembly.GetExecutingAssembly();

var tables = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(assembly, s => s.Contains("Tables"))
    .WithTransaction()
    .LogToConsole()
    .Build();

var tablesResult = tables.PerformUpgrade();
if (!tablesResult.Successful) return 1;

var indexes = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(assembly, s => s.Contains("Indexes"))
    .WithTransaction()
    .LogToConsole()
    .Build();

var indexesResult = indexes.PerformUpgrade();
return indexesResult.Successful ? 0 : 1;
