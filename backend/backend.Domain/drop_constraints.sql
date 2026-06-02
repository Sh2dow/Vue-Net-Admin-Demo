-- Drop foreign key constraint on orders table
ALTER TABLE orders DROP CONSTRAINT IF EXISTS fk_orders_app_users_user_id;

-- Drop foreign key constraint on task_comments table
ALTER TABLE task_comments DROP CONSTRAINT IF EXISTS fk_task_comments_app_users_author_id;

-- Drop foreign key constraint on task_comments table
ALTER TABLE task_comments DROP CONSTRAINT IF EXISTS fk_task_comments_tasks_task_id;

-- Drop foreign key constraint on tasks table
ALTER TABLE tasks DROP CONSTRAINT IF EXISTS fk_tasks_app_users_user_id;
