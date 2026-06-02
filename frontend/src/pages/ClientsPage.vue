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
    <v-card class="mb-4">
        <v-card-item>
            <template #title>
                <v-icon start icon="mdi-desktop-classic" />
                Clients
            </template>
            <template #subtitle>Read-only client info from your OIDC token</template>
        </v-card-item>
    </v-card>

    <v-card variant="outlined">
        <v-list v-if="Object.keys(clientInfo).length" lines="two">
            <v-list-item v-for="(value, key) in clientInfo" :key="key">
                <template #prepend>
                    <v-icon color="primary" icon="mdi-tag" />
                </template>
                <v-list-item-title class="font-weight-bold">{{ key }}</v-list-item-title>
                <v-list-item-subtitle class="font-mono text-body-2">{{ value }}</v-list-item-subtitle>
            </v-list-item>
        </v-list>
        <v-alert v-else type="info" variant="tonal">
            No client information found in your token claims.
        </v-alert>
    </v-card>
</template>
