# PowerShell script to mark migrations as applied for new contexts
# Run this before running EF migrations to skip applying migrations (tables already exist)

$connectionString = "Host=localhost;Port=5432;Database=vue_demo;Username=vue;Password=123"

Write-Host "Marking migrations as applied..."

# Use psql if available, otherwise use dotnet ef with a custom script
$psqlPath = "C:\Program Files\PostgreSQL\17\bin\psql.exe"

if (Test-Path $psqlPath) {
    $env:PGPASSWORD = "123"
    
    # TasksDbContext
    Write-Host "TasksDbContext..."
    & $psqlPath -h localhost -U vue -d vue_demo -c "INSERT INTO ""__EFMigrationsHistory"" (migration_id, product_version) VALUES ('20260322183300_Initial', '10.0.1') ON CONFLICT (migration_id) DO NOTHING;"
    
    # OrdersDbContext
    Write-Host "OrdersDbContext..."
    & $psqlPath -h localhost -U vue -d vue_demo -c "INSERT INTO ""__EFMigrationsHistory"" (migration_id, product_version) VALUES ('20260322183301_Initial', '10.0.1') ON CONFLICT (migration_id) DO NOTHING;"
    
    # PaymentsDbContext
    Write-Host "PaymentsDbContext..."
    & $psqlPath -h localhost -U vue -d vue_demo -c "INSERT INTO ""__EFMigrationsHistory"" (migration_id, product_version) VALUES ('20260322183302_Initial', '10.0.1') ON CONFLICT (migration_id) DO NOTHING;"
    
    # AuthDbContext
    Write-Host "AuthDbContext..."
    & $psqlPath -h localhost -U vue -d vue_demo_auth -c "INSERT INTO ""__EFMigrationsHistory"" (migration_id, product_version) VALUES ('20260322183303_Initial', '10.0.1') ON CONFLICT (migration_id) DO NOTHING;"
    
    Write-Host "Done!"
} else {
    Write-Host "psql not found. Please run the SQL script manually."
    Write-Host "SQL script location: \backend\backend.Domain\mark_all_migrations.sql"
}
