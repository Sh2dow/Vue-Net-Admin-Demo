-- Mark migrations as applied for new contexts
-- Run this SQL script to avoid EF trying to apply migrations (tables already exist)

-- TasksDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322183144_Initial', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;

-- OrdersDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322183149_Initial', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;

-- PaymentsDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322183155_Initial', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;

-- FixAppDbContextConfig migration
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322182716_FixAppDbContextConfig', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;
