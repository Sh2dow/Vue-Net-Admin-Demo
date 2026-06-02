import { createApp } from "vue";
import { createPinia } from "pinia";
import { createVuetify } from "vuetify";
import * as components from "vuetify/components";
import * as directives from "vuetify/directives";
import { VueQueryPlugin } from "@tanstack/vue-query";
import App from "./App.vue";
import router from "./router";

const app = createApp(App);

const vuetify = createVuetify({
    components,
    directives,
    theme: {
        defaultTheme: "light",
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
