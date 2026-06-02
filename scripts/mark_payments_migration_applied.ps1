$connectionString = "Host=localhost;Port=5432;Database=keycloak_demo_payments;Username=keycloak;Password=123"
$query = "INSERT INTO __efmigrationshistory (""MigrationId"", ""ProductVersion"") VALUES ('20260322184120_Initial', '10.0.5');"
$connectionString, $query | Invoke-PgSqlConnection
