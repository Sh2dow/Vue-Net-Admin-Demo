<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useAuth } from "../composables/useAuth";

const { checkAuth, login, isLoading: authLoading } = useAuth();
const loading = ref(false);

onMounted(async () => {
    const authenticated = await checkAuth();
    if (authenticated) {
        window.location.href = "/";
    }
});

async function handleLogin() {
    loading.value = true;
    try {
        await login();
    } finally {
        loading.value = false;
    }
}
</script>

<template>
    <v-app class="d-flex align-center justify-center fill-height">
        <v-card width="400" class="pa-8" elevation="8">
            <v-card-item class="text-center">
                <v-icon size="64" color="primary" icon="mdi-shield-account" class="mb-4" />
                <h2 class="text-h5 mb-2">Sign In</h2>
                <p class="text-body-2 text-medium-emphasis">
                    Authenticate with OpenIddict to access the admin dashboard.
                </p>
            </v-card-item>
            <v-card-actions class="justify-center pa-4 pt-8">
                <v-btn
                    color="primary"
                    size="large"
                    block
                    loading="false"
                    :disabled="loading || authLoading"
                    prepend-icon="mdi-login"
                    @click="handleLogin"
                >
                    Login with OpenIddict
                </v-btn>
            </v-card-actions>
        </v-card>
    </v-app>
</template>
