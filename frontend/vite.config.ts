import { defineConfig, loadEnv } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), "");
    const apiProxyTarget = env.VITE_API_PROXY_TARGET || "http://localhost:5000";
    const ordersProxyTarget = env.VITE_ORDERS_PROXY_TARGET || "http://localhost:5003";
    const tasksProxyTarget = env.VITE_TASKS_PROXY_TARGET || "http://localhost:5002";

    return {
        plugins: [vue()],
        server: {
            port: parseInt(env.VITE_PORT) || 5173,
            proxy: {
                "/api/users": apiProxyTarget,
                "/api/orders": ordersProxyTarget,
                "/api/tasks": tasksProxyTarget,
                "/connect": apiProxyTarget,
                "/.well-known": { target: env.VITE_AUTHORITY || "http://localhost:5001", changeOrigin: true },
                "/connect/token": { target: env.VITE_AUTHORITY || "http://localhost:5001", changeOrigin: true },
                "/connect/authorize": { target: env.VITE_AUTHORITY || "http://localhost:5001", changeOrigin: true },
                "/connect/logout": { target: env.VITE_AUTHORITY || "http://localhost:5001", changeOrigin: true },
                "/connect/revoke": { target: env.VITE_AUTHORITY || "http://localhost:5001", changeOrigin: true },
            },
        },
    };
});
