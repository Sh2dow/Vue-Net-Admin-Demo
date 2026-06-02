-- Fake migration history for TasksDbContext
INSERT INTO __efmigrationshistory (MigrationId, ProductVersion) 
VALUES ('20260322182108_InitialTasks', '10.0.1')
ON CONFLICT (MigrationId) DO NOTHING;

-- Fake migration history for OrdersDbContext
INSERT INTO __efmigrationshistory (MigrationId, ProductVersion) 
VALUES ('20260322182114_InitialOrders', '10.0.1')
ON CONFLICT (MigrationId) DO NOTHING;

-- Fake migration history for PaymentsDbContext
INSERT INTO __efmigrationshistory (MigrationId, ProductVersion) 
VALUES ('20260322182119_InitialPayments', '10.0.1')
ON CONFLICT (MigrationId) DO NOTHING;
