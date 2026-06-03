import { createRouter, createWebHistory, type NavigationGuardNext } from "vue-router";
import { useAuth } from "../composables/useAuth";

const routes = [
    {
        path: "/login",
        name: "Login",
        component: () => import("../pages/LoginPage.vue"),
        meta: { public: true },
    },
    {
        path: "/",
        component: () => import("../layouts/DefaultLayout.vue"),
        children: [
            { path: "", redirect: () => ({ name: "Users" }) },
            {
                path: "users",
                name: "Users",
                component: () => import("../pages/UsersPage.vue"),
            },
            {
                path: "orders",
                name: "Orders",
                component: () => import("../pages/OrdersPage.vue"),
            },
            {
                path: "orders/:id",
                name: "OrderDetails",
                component: () => import("../pages/OrderDetailsPage.vue"),
            },
            {
                path: "tasks",
                name: "Tasks",
                component: () => import("../pages/TasksPage.vue"),
            },
            {
                path: "roles",
                name: "Roles",
                component: () => import("../pages/RolesPage.vue"),
            },
            {
                path: "groups",
                name: "Groups",
                component: () => import("../pages/GroupsPage.vue"),
            },
            {
                path: "clients",
                name: "Clients",
                component: () => import("../pages/ClientsPage.vue"),
            },
        ],
    },
    {
        path: "/:pathMatch(.*)*",
        redirect: () => ({ name: "Users" }),
    },
];

const router = createRouter({
    history: createWebHistory(),
    routes,
});

// Flag to track whether initial auth check completed
let authCheckDone = false;
let pendingCheck: Promise<boolean> | null = null;

async function ensureAuthCheck(): Promise<boolean> {
    if (authCheckDone) {
        const { isAuthenticated } = useAuth();
        return isAuthenticated.value;
    }
    if (!pendingCheck) {
        pendingCheck = new Promise(async (resolve) => {
            const { checkAuth } = useAuth();
            const result = await checkAuth();
            authCheckDone = true;
            pendingCheck = null;
            resolve(result);
        });
    }
    return pendingCheck;
}

// Navigation guard: check auth before entering protected routes
router.beforeEach(async (to, _from, next: NavigationGuardNext) => {
    // Check if this is an OIDC callback (code + state params)
    const hasAuthParams = to.query.code && to.query.state;

    if (hasAuthParams) {
        // Process the OIDC callback first, before any route logic
        const authenticated = await ensureAuthCheck();

        if (authenticated) {
            // Navigate to the app root, clearing the callback params
            return next("/");
        }

        // Callback failed, redirect to clean login page
        return next({ path: "/login", query: {} });
    }

    if (to.meta.public) {
        // Wait for auth check to complete so isLoading is properly set to false
        const authenticated = await ensureAuthCheck();

        if (authenticated) {
            return next({ name: "Users" });
        }
        return next();
    }

    // Ensure the auth check has completed
    const authenticated = await ensureAuthCheck();

    if (!authenticated) {
        return next({ name: "Login" });
    }

    return next();
});

export default router;
