-- Manually record the migration as applied since the table already exists
INSERT INTO __efmigrationshistory ("MigrationId", "ProductVersion") VALUES ('20260323163218_InitAuthDbContext', '10.0.5');
