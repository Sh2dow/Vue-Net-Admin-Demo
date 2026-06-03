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
    <!-- Header -->
    <div class="d-flex align-center mb-6">
        <div class="d-flex align-center">
            <div class="icon-box mr-3">
                <v-icon size="24" color="white" icon="mdi-shield-account" />
            </div>
            <div>
                <h1 class="text-h5 font-weight-bold mb-1">Roles</h1>
                <p class="text-body-2 text-medium-emphasis mb-0">Read-only roles from Tasks API</p>
            </div>
        </div>
    </div>

    <!-- Roles grid -->
    <v-card elevation="1" rounded="xl">
        <v-list v-if="!loading" lines="two">
            <v-list-item v-for="role in roles" :key="role" class="hover-row">
                <template #prepend>
                    <v-avatar color="primary" variant="tonal" size="small" rounded="lg">
                        <v-icon size="18" icon="mdi-shield-account" />
                    </v-avatar>
                </template>
                <v-list-item-title class="font-weight-medium">{{ role }}</v-list-item-title>
            </v-list-item>
        </v-list>
        <v-progress-linear v-else indeterminate color="primary" />
        <div v-if="!loading && roles.length === 0" class="text-center text-medium-emphasis py-8">
            <v-icon size="large" color="medium-emphasis" icon="mdi-shield-off" class="mb-2" />
            <div>No roles found.</div>
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
