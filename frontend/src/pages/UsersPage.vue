<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { usersApi, type AppUser } from "../api";

const router = useRouter();
const users = ref<AppUser[]>([]);
const loading = ref(false);
const creating = ref(false);
const deleting = ref(false);
const editing = ref<AppUser | null>(null);
const editUsername = ref("");
const editEmail = ref("");

const subject = ref("");
const username = ref("");
const email = ref("");

const sortedUsers = computed(() => [...users.value].sort((a, b) => a.username.localeCompare(b.username)));

async function fetchUsers() {
    loading.value = true;
    try {
        const res = await usersApi.list();
        users.value = res.data;
    } finally {
        loading.value = false;
    }
}

async function createUser() {
    if (!subject.value.trim() || !username.value.trim()) return;
    creating.value = true;
    try {
        await usersApi.create({ subject: subject.value.trim(), username: username.value.trim(), email: email.value.trim() || null });
        subject.value = "";
        username.value = "";
        email.value = "";
        await fetchUsers();
    } finally {
        creating.value = false;
    }
}

async function updateUser() {
    if (!editing.value || !editUsername.value.trim()) return;
    await usersApi.update(editing.value.id, { username: editUsername.value.trim(), email: editEmail.value.trim() || null });
    editing.value = null;
    await fetchUsers();
}

async function deleteUser(id: string) {
    deleting.value = true;
    try {
        await usersApi.delete(id);
        await fetchUsers();
    } finally {
        deleting.value = false;
    }
}

function openEdit(user: AppUser) {
    editing.value = user;
    editUsername.value = user.username;
    editEmail.value = user.email ?? "";
}

function exploreOrders(id: string) {
    router.push({ name: "Orders", query: { asUserId: id } });
}

function exploreTasks(id: string) {
    router.push({ name: "Tasks", query: { asUserId: id } });
}

onMounted(fetchUsers);
</script>

<template>
    <v-card class="mb-4">
        <v-card-item>
            <template #title>
                <v-icon start icon="mdi-account-group" />
                Users
            </template>
        </v-card-item>
    </v-card>

    <!-- Create form -->
    <v-card class="mb-4" variant="outlined">
        <v-card-item>
            <v-card-title class="text-subtitle-1 font-weight-bold">Create User</v-card-title>
            <v-row class="mt-2">
                <v-col cols="12" sm="4">
                    <v-text-field v-model="subject" label="Subject" placeholder="keycloak-sub" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="12" sm="4">
                    <v-text-field v-model="username" label="Username" placeholder="johndoe" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="12" sm="4">
                    <v-text-field v-model="email" label="Email" placeholder="user@example.com" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="12" class="text-right">
                    <v-btn color="primary" :loading="creating" @click="createUser">
                        <v-icon start icon="mdi-plus" />
                        Create
                    </v-btn>
                </v-col>
            </v-row>
        </v-card-item>
    </v-card>

    <!-- Table -->
    <v-card variant="outlined" class="mb-4">
        <v-table v-if="!loading">
            <thead>
                <tr>
                    <th class="text-start">Username</th>
                    <th>Subject</th>
                    <th>Email</th>
                    <th>Orders</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
                <tr v-for="user in sortedUsers" :key="user.id">
                    <td>{{ user.username }}</td>
                    <td class="text-body-2 text-medium-emphasis">{{ user.subject }}</td>
                    <td>{{ user.email ?? "-" }}</td>
                    <td>{{ user.orders?.length ?? 0 }}</td>
                    <td>
                        <v-btn size="x-small" variant="text" class="mr-1" @click="openEdit(user)">Edit</v-btn>
                        <v-btn size="x-small" color="teal" variant="text" class="mr-1" @click="exploreOrders(user.id)">Explore Orders</v-btn>
                        <v-btn size="x-small" color="cyan" variant="text" class="mr-1" @click="exploreTasks(user.id)">Explore Tasks</v-btn>
                        <v-btn size="x-small" color="error" variant="text" :loading="deleting" @click="deleteUser(user.id)">Delete</v-btn>
                    </td>
                </tr>
            </tbody>
        </v-table>
        <v-progress-linear v-else indeterminate color="primary" />
    </v-card>

    <!-- Orders per user -->
    <template v-if="!loading">
        <v-card v-for="user in sortedUsers" :key="'orders-' + user.id" class="mb-4" variant="outlined">
            <v-card-item>
                <v-row align="center" no-gutters>
                    <v-col cols="8">
                        <div class="font-weight-bold">{{ user.username }}</div>
                    </v-col>
                    <v-col cols="4" class="text-right">
                        <v-chip size="small" variant="tonal">{{ user.orders?.length ?? 0 }} orders</v-chip>
                    </v-col>
                </v-row>
                <v-divider class="my-2" />
                <div v-if="user.orders?.length === 0" class="text-body-2 text-medium-emphasis pa-2">No orders</div>
                <div v-else class="pa-2">
                    <div v-for="order in user.orders" :key="order.id" class="text-body-2 mb-1">
                        {{ order.orderType.toUpperCase() }} | ${{ order.totalAmount.toFixed(2) }} | {{ order.status }}
                    </div>
                </div>
            </v-card-item>
        </v-card>
    </template>

    <!-- Edit modal -->
    <v-dialog :model-value="!!editing" max-width="500" persistent @update:model-value="editing = null">
        <v-card>
            <v-card-item>
                <v-card-title>Edit User</v-card-title>
            </v-card-item>
            <v-card-text>
                <v-text-field v-model="editUsername" label="Username" variant="outlined" density="compact" class="mb-2" />
                <v-text-field v-model="editEmail" label="Email" variant="outlined" density="compact" />
            </v-card-text>
            <v-card-actions>
                <v-spacer />
                <v-btn variant="text" @click="editing = null">Cancel</v-btn>
                <v-btn color="primary" @click="updateUser">Save</v-btn>
            </v-card-actions>
        </v-card>
    </v-dialog>
</template>
