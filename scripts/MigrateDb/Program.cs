using Npgsql;

var pgHost = Environment.GetEnvironmentVariable("PG_HOST") ?? "postgresql-3l73qz2t2b7bo";
var pgUser = Environment.GetEnvironmentVariable("PG_USER") ?? "vueadmin";
var pgPassword = Environment.GetEnvironmentVariable("PG_PASSWORD") ?? "VueAdmin2025Secure";
var databases = new[] { "vue_demo_auth", "vue_demo_orders", "vue_demo_payments", "vue_demo_tasks" };

async Task CreateDatabase(string name)
{
    var connStr = $"Host={pgHost};Port=5432;Username={pgUser};Password={pgPassword};Database=postgres";
    await using var conn = new NpgsqlConnection(connStr);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{name}'", conn);
    var exists = await cmd.ExecuteScalarAsync() is not null;
    if (exists)
    {
        Console.WriteLine($"Database '{name}' already exists.");
        return;
    }
    await using var createCmd = new NpgsqlCommand($"CREATE DATABASE {name}", conn);
    await createCmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Database '{name}' created.");
}

Console.WriteLine($"Connecting to PostgreSQL at {pgHost}...");
try
{
    foreach (var db in databases)
    {
        await CreateDatabase(db);
    }
    Console.WriteLine("All databases created successfully.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
