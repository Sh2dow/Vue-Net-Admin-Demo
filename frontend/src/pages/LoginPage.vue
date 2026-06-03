<script setup lang="ts">
import { ref } from "vue";
import { useAuth } from "../composables/useAuth";

const { login, isLoading: authLoading } = useAuth();
const loading = ref(false);

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
    <v-app class="login-page">
        <v-container class="fill-height d-flex align-center justify-center">
            <v-card
                class="login-card pa-8 pa-sm-10"
                width="440"
                elevation="12"
                rounded="xl"
            >
                <!-- Logo -->
                <div class="text-center mb-6">
                    <div class="logo-circle mx-auto mb-4 d-flex align-center justify-center">
                        <v-icon size="56" color="white" icon="mdi-speedometer" />
                    </div>
                    <h1 class="text-h4 font-weight-bold mb-1">Welcome back</h1>
                    <p class="text-body-1 text-medium-emphasis">
                        Sign in to your admin dashboard
                    </p>
                </div>

                <!-- Login button -->
                <v-btn
                    color="primary"
                    size="x-large"
                    block
                    :loading="loading || authLoading"
                    :disabled="loading || authLoading"
                    prepend-icon="mdi-login"
                    class="text-subtitle-1 font-weight-medium"
                    rounded="lg"
                    elevation="4"
                    @click="handleLogin"
                >
                    Continue with OpenIddict
                </v-btn>

                <!-- Footer -->
                <div class="text-center mt-8">
                    <v-icon size="small" color="medium-emphasis" icon="mdi-shield-check" />
                    <span class="text-caption text-medium-emphasis ml-2">Secured by OpenIddict OIDC</span>
                </div>
            </v-card>
        </v-container>
    </v-app>
</template>

<style scoped>
.login-page {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.login-card {
    border: 1px solid rgba(255, 255, 255, 0.15);
    backdrop-filter: blur(10px);
}

.logo-circle {
    width: 96px;
    height: 96px;
    border-radius: 50%;
    background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
    box-shadow: 0 8px 24px rgba(99, 102, 241, 0.35);
}
</style>
