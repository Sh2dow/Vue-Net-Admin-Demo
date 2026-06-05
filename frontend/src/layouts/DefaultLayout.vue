<script setup lang="ts">
import { ref } from "vue";
import { useRoute } from "vue-router";
import { useAuth } from "../composables/useAuth";

const route = useRoute();
const { userName, logout } = useAuth();
const drawer = ref(true);

const navItems = [
    { title: "Users", to: "Users", icon: "mdi-account-group", pattern: /^\/users/ },
    { title: "Orders", to: "Orders", icon: "mdi-cart-variant", pattern: /^\/orders/ },
    { title: "Tasks", to: "Tasks", icon: "mdi-checkbox-marked-outline", pattern: /^\/tasks/ },
    { title: "Roles", to: "Roles", icon: "mdi-shield-account", pattern: /^\/roles/ },
    { title: "Groups", to: "Groups", icon: "mdi-account-multiple", pattern: /^\/groups/ },
    { title: "Clients", to: "Clients", icon: "mdi-laptop", pattern: /^\/clients/ },
];

function isNavActive(pattern: RegExp) {
    return pattern.test(route.path);
}

async function handleLogout() {
    await logout();
}
</script>

<template>
    <v-app>
        <!-- Top bar -->
        <v-app-bar color="primary" flat>
            <v-btn
                icon="mdi-menu"
                variant="text"
                class="mr-2"
                size="small"
                @click="drawer = !drawer"
            />
            <v-app-bar-title class="font-weight-bold text-white">
                <v-icon start icon="mdi-speedometer" color="white" />
                Admin Dashboard
            </v-app-bar-title>
            <v-spacer />

            <!-- User dropdown -->
            <v-menu
                location="bottom"
                transition="scale-transition"
            >
                <template #activator="{ props }">
                    <v-btn
                        v-bind="props"
                        variant="tonal"
                        color="white"
                        prepend-icon="mdi-account"
                        size="small"
                        class="text-primary mx-2"
                    >
                        {{ userName }}
                    </v-btn>
                </template>
                <v-sheet rounded="lg" class="pa-4" width="200">
                    <div class="text-subtitle-1 font-weight-medium mb-1">{{ userName }}</div>
                    <div class="text-caption text-medium-emphasis">Administrator</div>
                    <v-divider class="my-2" />
                    <v-btn
                        block
                        color="error"
                        variant="tonal"
                        size="small"
                        prepend-icon="mdi-logout"
                        @click="handleLogout"
                    >
                        Sign out
                    </v-btn>
                </v-sheet>
            </v-menu>
        </v-app-bar>

        <!-- Sidebar -->
        <v-navigation-drawer
            v-model="drawer"
            rail
            rail-width="68"
            width="260"
            class="layout-drawer"
            expand-on-hover
        >
            <!-- Drawer header -->
            <div class="drawer-header pa-4 d-flex align-center">
                <v-icon size="32" color="white" icon="mdi-speedometer" />
                <div v-if="!drawer" class="text-body-2 text-white ml-2 font-weight-bold">Admin</div>
            </div>

            <v-divider />

            <!-- Navigation -->
            <v-list density="compact" nav>
                <v-list-item
                    v-for="item in navItems"
                    :key="item.to"
                    :to="{ name: item.to }"
                    :value="item.to"
                    :active="isNavActive(item.pattern)"
                    rounded="lg"
                    class="mb-1 ma-1"
                    color="primary"
                >
                    <template #prepend>
                        <v-icon :icon="item.icon" />
                    </template>
                    <v-list-item-title>{{ item.title }}</v-list-item-title>
                </v-list-item>
            </v-list>
        </v-navigation-drawer>

        <!-- Main content -->
        <v-main class="background">
            <v-container fluid class="pa-4 pa-sm-6 pa-md-8" style="max-width: 1400px">
                <router-view />
            </v-container>
        </v-main>
    </v-app>
</template>

<style>
.v-toolbar__content {
    overflow: visible !important;
}
</style>

<style scoped>
.layout-drawer {
    box-shadow: 2px 0 8px rgba(0, 0, 0, 0.04) !important;
}

.drawer-header {
    background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
}

.background {
    background: #f8fafc;
}
</style>
