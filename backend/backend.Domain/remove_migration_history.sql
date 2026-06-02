-- Remove migration history records for the new contexts
DELETE FROM "__EFMigrationsHistory" 
WHERE migration_id IN (
    '20260322182108_InitialTasks',
    '20260322182114_InitialOrders', 
    '20260322182119_InitialPayments'
);
