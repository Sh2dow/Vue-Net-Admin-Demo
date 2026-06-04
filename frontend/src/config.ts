// Runtime config loaded from /config.json (served by nginx).
// Falls back to build-time env vars for local dev.

export interface AppConfig {
    authority: string;
    apiUrl: string;
}

let configPromise: Promise<AppConfig> | null = null;

export async function loadConfig(): Promise<AppConfig> {
    if (configPromise) return configPromise;

    configPromise = (async () => {
        try {
            const res = await fetch('/config.json');
            if (res.ok) {
                const data = await res.json();
                console.log('[config] Loaded /config.json:', data);
                return data;
            }
        } catch (e) {
            console.warn('[config] Failed to fetch /config.json:', e);
        }
        const fallback = {
            authority: import.meta.env.VITE_AUTHORITY ?? window.location.origin,
            apiUrl: import.meta.env.VITE_API_URL ?? window.location.origin,
        };
        console.warn('[config] Using fallback:', fallback);
        return fallback;
    })();

    return configPromise;
}
