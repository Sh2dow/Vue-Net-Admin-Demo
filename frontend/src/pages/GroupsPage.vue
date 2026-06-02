<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useAuth } from "../composables/useAuth";

const { user } = useAuth();
const groups = ref<string[]>([]);

onMounted(() => {
    const profile = user.value?.profile;
    if (profile && Array.isArray(profile.groups)) {
        groups.value = profile.groups;
    }
});
</script>

<template>
    <v-card class="mb-4">
        <v-card-item>
            <template #title>
                <v-icon start icon="mdi-account-group" />
                Groups
            </template>
            <template #subtitle>Read-only groups from your OIDC token</template>
        </v-card-item>
    </v-card>

    <v-card variant="outlined">
        <v-list v-if="groups.length" lines="two">
            <v-list-item v-for="(group, i) in groups" :key="group">
                <template #prepend>
                    <v-avatar color="primary" variant="tonal" size="small">{{ i + 1 }}</v-avatar>
                </template>
                {{ group }}
            </v-list-item>
        </v-list>
        <v-alert v-else type="info" variant="tonal">
            No groups found in your token profile.
        </v-alert>
    </v-card>
</template>
