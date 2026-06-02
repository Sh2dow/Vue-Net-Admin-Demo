<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useAuth } from "../composables/useAuth";

const route = useRoute();
const { userName, logout } = useAuth();

const navItems = [
    { title: "Users", to: "Users", icon: "mdi-account-group" },
    { title: "Orders", to: "Orders", icon: "mdi-cart-variant" },
    { title: "Tasks", to: "Tasks", icon: "mdi-checkbox-marked-outline" },
    { title: "Roles", to: "Roles", icon: "mdi-shield-account" },
    { title: "Groups", to: "Groups", icon: "mdi-account-multiple" },
    { title: "Clients", to: "Clients", icon: "mdi-laptop" },
];

const isOrdersActive = computed(() => route.path.startsWith("/orders"));

async function handleLogout() {
    await logout();
}
</script>

<template>
    <v-app>
        <!-- Top bar -->
        <v-app-bar elevated>
            <v-app-bar-title class="font-weight-bold">.Net Crud Demo Dashboard</v-app-bar-title>
            <v-spacer />
            <v-btn variant="text" prepend-icon="mdi-account" size="small">
                {{ userName }}
            </v-btn>
            <v-btn variant="tonal" color="error" prepend-icon="mdi-logout" size="small" @click="handleLogout">
                Logout
            </v-btn>
        </v-app-bar>

        <!-- Sidebar -->
        <v-navigation-drawer permanent>
            <v-list density="compact" nav>
                <v-list-item
                    v-for="item in navItems"
                    :key="item.to"
                    :to="{ name: item.to }"
                    :value="item.to"
                    :active="item.to === 'Orders' ? isOrdersActive : $route.name === item.to"
                    rounded
                    class="mb-1"
                >
                    <template #prepend>
                        <v-icon :icon="item.icon" />
                    </template>
                    <v-list-item-title>{{ item.title }}</v-list-item-title>
                </v-list-item>
            </v-list>
        </v-navigation-drawer>

        <!-- Main content -->
        <v-main class="pa-4">
            <v-container fluid class="pa-0" style="max-width: 1200px">
                <router-view />
            </v-container>
        </v-main>
    </v-app>
</template>
