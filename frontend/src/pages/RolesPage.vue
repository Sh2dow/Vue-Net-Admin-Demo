<script setup lang="ts">
import { ref, onMounted } from "vue";
import { rolesApi } from "../api";

const roles = ref<string[]>([]);
const loading = ref(false);

onMounted(async () => {
    loading.value = true;
    try {
        const res = await rolesApi.list();
        roles.value = res.data;
    } finally {
        loading.value = false;
    }
});
</script>

<template>
    <v-card class="mb-4">
        <v-card-item>
            <template #title>
                <v-icon start icon="mdi-shield-account-variant" />
                Roles
            </template>
            <template #subtitle>Read-only roles from Tasks API</template>
        </v-card-item>
    </v-card>

    <v-card variant="outlined">
        <v-list v-if="!loading" lines="two">
            <v-list-item v-for="(role, i) in roles" :key="role">
                <template #prepend>
                    <v-avatar color="primary" variant="tonal" size="small">{{ i + 1 }}</v-avatar>
                </template>
                {{ role }}
            </v-list-item>
        </v-list>
        <v-progress-linear v-else indeterminate color="primary" />
    </v-card>
</template>
