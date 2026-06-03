<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useAuth } from "../composables/useAuth";

const { user } = useAuth();
const clientInfo = ref<Record<string, string>>({});

onMounted(() => {
    const profile = user.value?.profile;
    if (profile) {
        if (profile.azp) clientInfo.value["Client (azp)"] = profile.azp;
        if (profile.aud) clientInfo.value["Audience (aud)"] = Array.isArray(profile.aud) ? profile.aud.join(", ") : profile.aud;
    }
});
</script>

<template>
    <!-- Header -->
    <div class="d-flex align-center mb-6">
        <div class="d-flex align-center">
            <div class="icon-box mr-3">
                <v-icon size="24" color="white" icon="mdi-laptop" />
            </div>
            <div>
                <h1 class="text-h5 font-weight-bold mb-1">Clients</h1>
                <p class="text-body-2 text-medium-emphasis mb-0">Read-only client info from your OIDC token</p>
            </div>
        </div>
    </div>

    <!-- Client info cards -->
    <v-card elevation="1" rounded="xl">
        <v-list v-if="Object.keys(clientInfo).length" lines="two">
            <v-list-item v-for="(value, key) in clientInfo" :key="key" class="hover-row">
                <template #prepend>
                    <v-avatar color="primary" variant="tonal" size="small" rounded="lg">
                        <v-icon size="18" icon="mdi-tag" />
                    </v-avatar>
                </template>
                <v-list-item-title class="font-weight-medium">{{ key }}</v-list-item-title>
                <v-list-item-subtitle class="font-mono text-body-2">{{ value }}</v-list-item-subtitle>
            </v-list-item>
        </v-list>
        <div v-else class="text-center text-medium-emphasis py-8">
            <v-icon size="large" color="medium-emphasis" icon="mdi-laptop-off" class="mb-2" />
            <div>No client information found in your token claims.</div>
        </div>
    </v-card>
</template>

<style scoped>
.icon-box {
    width: 44px;
    height: 44px;
    border-radius: 12px;
    background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
    display: flex;
    align-items: center;
    justify-content: center;
}

.hover-row:hover {
    background: #f8fafc;
}
</style>
