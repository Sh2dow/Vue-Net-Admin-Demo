-- Mark migrations as applied for new contexts
-- Run this SQL script before running EF migrations

-- TasksDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322183200_Initial', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;

-- OrdersDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322183201_Initial', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;

-- PaymentsDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) 
VALUES ('20260322183202_Initial', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;
