-- Insert migration records for each context without running migrations (tables already exist from old AppDbContext)

-- TasksDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322182108_InitialTasks', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;

-- OrdersDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322182114_InitialOrders', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;

-- PaymentsDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322182119_InitialPayments', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;

-- FixAppDbContextConfig migration
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322182716_FixAppDbContextConfig', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;
