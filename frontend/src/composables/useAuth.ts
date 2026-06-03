import { UserManager, type UserManagerSettings, type User } from "oidc-client-ts";
import { ref, computed } from "vue";
import type { Ref } from "vue";

const appOrigin = window.location.origin;
const authority = import.meta.env.VITE_AUTHORITY ?? "https://localhost:5201";

const oidcSettings: UserManagerSettings = {
    authority,
    client_id: "vue-client",
    redirect_uri: `${appOrigin}/`,
    post_logout_redirect_uri: `${appOrigin}/login`,
    response_type: "code",
    scope: "openid profile email roles offline_access",
    automaticSilentRenew: true,
    filterProtocolClaims: true,
};

export const userManager = new UserManager(oidcSettings);

// Reactive user state shared across the app
export const user: Ref<User | null> = ref(null);
export const isLoading = ref(true);

export const isAuthenticated = computed(() => user.value !== null && !user.value.expired);
export const userName = computed(() => user.value?.profile?.preferred_username ?? user.value?.profile?.name ?? "Unknown");
export const userEmail = computed(() => user.value?.profile?.email ?? "");
export const userRoles = computed(() => {
    const roles = user.value?.profile?.roles;
    if (Array.isArray(roles)) return roles;
    if (typeof roles === "string") return roles.split(",").map((r) => r.trim());
    return [];
});

export function useAuth() {
    /** Log in via OIDC redirect */
    async function login(): Promise<void> {
        await userManager.signinRedirect();
    }

    /** Handle callback after OIDC redirect */
    async function handleCallback(): Promise<User | null> {
        try {
            const result = await userManager.signinRedirectCallback();
            user.value = result;
            // Clean URL after callback
            window.history.replaceState({}, document.title, "/");
            return result;
        } catch {
            return null;
        }
    }

    /** Log out and redirect */
    async function logout(): Promise<void> {
        await userManager.signoutRedirect();
    }

    /** Check authentication state */
    async function checkAuth(): Promise<boolean> {
        isLoading.value = true;
        try {
            const callbackUrl = new URL(window.location.href);
            const hasAuthParams = callbackUrl.searchParams.has("code") && callbackUrl.searchParams.has("state");

            if (hasAuthParams) {
                await handleCallback();
            } else {
                // Silent check
                const fetchedUser = await userManager.getUser();
                user.value = fetchedUser;
            }

            return isAuthenticated.value;
        } finally {
            isLoading.value = false;
        }
    }

    /** Get access token for API calls */
    async function getAccessToken(): Promise<string | null> {
        const fetchedUser = await userManager.getUser();
        if (!fetchedUser || fetchedUser.expired) {
            user.value = null;
            return null;
        }
        user.value = fetchedUser;
        return fetchedUser.access_token;
    }

    /** Get roles from the current user */
    function getRoles(): string[] {
        return userRoles.value;
    }

    return {
        user,
        isLoading,
        isAuthenticated,
        userName,
        userEmail,
        userRoles,
        login,
        logout,
        checkAuth,
        getAccessToken,
        getRoles,
        handleCallback,
    };
}
