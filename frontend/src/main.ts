import { createApp } from "vue";
import { createPinia } from "pinia";
import { createVuetify } from "vuetify";
import { mdi } from "vuetify/iconsets/mdi";
import * as components from "vuetify/components";
import * as directives from "vuetify/directives";
import "vuetify/styles";
import "@mdi/font/css/materialdesignicons.css";
import { VueQueryPlugin } from "@tanstack/vue-query";
import App from "./App.vue";
import router from "./router";

const app = createApp(App);

const vuetify = createVuetify({
    components,
    directives,
    icons: {
        defaultSet: "mdi",
        sets: { mdi },
    },
    theme: {
        defaultTheme: "light",
        themes: {
            light: {
                dark: false,
                colors: {
                    primary: "#6366F1",
                    "primary-darken1": "#4F46E5",
                    secondary: "#8B5CF6",
                    accent: "#F59E0B",
                    success: "#10B981",
                    info: "#3B82F6",
                    warning: "#F59E0B",
                    error: "#EF4444",
                    background: "#F1F5F9",
                    surface: "#FFFFFF",
                    "surface-bright": "#F8FAFC",
                    "surface-dim": "#E2E8F0",
                    "on-primary": "#FFFFFF",
                    "on-surface": "#1E293B",
                },
                variables: {
                    "border-radius": "8px",
                    "border-color": "#E2E8F0",
                    "shadow-opacity": 0.08,
                },
            },
        },
    },
});

app.use(createPinia());
app.use(router);
app.use(VueQueryPlugin, {
    queryClientConfig: {
        defaultOptions: {
            queries: {
                staleTime: 10_000,
                retry: 1,
            },
        },
    },
});
app.use(vuetify);

app.mount("#app");
