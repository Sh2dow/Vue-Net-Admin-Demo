-- Mark all migrations as applied for PaymentsDbContext
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) VALUES 
('20260322183024_Initial', '10.0.1')
ON CONFLICT (migration_id) DO NOTHING;
