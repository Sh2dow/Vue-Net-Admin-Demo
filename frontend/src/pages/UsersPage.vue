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
const showCreateForm = ref(false);

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
        showCreateForm.value = false;
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
    <!-- Header -->
    <div class="d-flex align-center mb-6">
        <div class="d-flex align-center">
            <div class="icon-box mr-3">
                <v-icon size="24" color="white" icon="mdi-account-group" />
            </div>
            <div>
                <h1 class="text-h5 font-weight-bold mb-1">Users</h1>
                <p class="text-body-2 text-medium-emphasis mb-0">Manage application users and accounts</p>
            </div>
        </div>
        <v-spacer />
        <v-btn
            color="primary"
            variant="elevated"
            prepend-icon="mdi-plus"
            rounded="lg"
            @click="showCreateForm = !showCreateForm"
        >
            {{ showCreateForm ? "Cancel" : "New User" }}
        </v-btn>
        <v-btn
            icon="mdi-refresh"
            variant="text"
            size="small"
            :loading="loading"
            class="ml-2"
            @click="fetchUsers"
        />
    </div>

    <!-- Create form -->
    <v-expand-transition>
        <v-card v-if="showCreateForm" class="mb-6" elevation="1" rounded="xl">
            <v-card-item class="pa-6">
                <v-card-title class="text-subtitle-1 font-weight-bold mb-4">
                    <v-icon start icon="mdi-account-plus" color="primary" />
                    Create new user
                </v-card-title>
                <v-row>
                    <v-col cols="12" sm="4">
                        <v-text-field v-model="subject" label="Subject ID" placeholder="e.g. abc-123" variant="outlined" density="comfortable" />
                    </v-col>
                    <v-col cols="12" sm="4">
                        <v-text-field v-model="username" label="Username" placeholder="johndoe" variant="outlined" density="comfortable" />
                    </v-col>
                    <v-col cols="12" sm="4">
                        <v-text-field v-model="email" label="Email" placeholder="user@example.com" variant="outlined" density="comfortable" />
                    </v-col>
                    <v-col cols="12" class="text-right">
                        <v-btn color="primary" :loading="creating" rounded="lg" @click="createUser">
                            <v-icon start icon="mdi-check" />
                            Create user
                        </v-btn>
                    </v-col>
                </v-row>
            </v-card-item>
        </v-card>
    </v-expand-transition>

    <!-- Users table -->
    <v-card elevation="1" rounded="xl">
        <v-table v-if="!loading" class="text-no-wrap">
            <thead>
                <tr>
                    <th class="text-start text-subtitle-2 font-weight-bold">Username</th>
                    <th class="text-subtitle-2 font-weight-bold">Subject</th>
                    <th class="text-subtitle-2 font-weight-bold">Email</th>
                    <th class="text-subtitle-2 font-weight-bold">Orders</th>
                    <th class="text-subtitle-2 font-weight-bold">Actions</th>
                </tr>
            </thead>
            <tbody>
                <tr v-for="user in sortedUsers" :key="user.id" class="hover-row">
                    <td>
                        <div class="d-flex align-center">
                            <v-avatar size="32" color="primary" variant="tonal" class="mr-3">
                                <span class="text-subtitle-2 font-weight-bold">{{ user.username.charAt(0).toUpperCase() }}</span>
                            </v-avatar>
                            <span class="font-weight-medium">{{ user.username }}</span>
                        </div>
                    </td>
                    <td>
                        <v-chip size="small" variant="flat" color="surface-dim" class="font-mono">
                            {{ user.subject }}
                        </v-chip>
                    </td>
                    <td>{{ user.email ?? "-" }}</td>
                    <td>
                        <v-chip v-if="user.orders?.length" size="small" color="primary" variant="tonal">
                            {{ user.orders.length }}
                        </v-chip>
                        <span v-else class="text-medium-emphasis">—</span>
                    </td>
                    <td>
                        <v-btn size="x-small" color="primary" variant="text" class="mr-1" @click="openEdit(user)">
                            <v-icon start size="16" icon="mdi-pencil" />
                            Edit
                        </v-btn>
                        <v-btn size="x-small" color="info" variant="text" class="mr-1" @click="exploreOrders(user.id)">
                            <v-icon start size="16" icon="mdi-cart-outline" />
                            Orders
                        </v-btn>
                        <v-btn size="x-small" color="success" variant="text" class="mr-1" @click="exploreTasks(user.id)">
                            <v-icon start size="16" icon="mdi-checkbox-marked-outline" />
                            Tasks
                        </v-btn>
                        <v-btn size="x-small" color="error" variant="text" :loading="deleting" @click="deleteUser(user.id)">
                            <v-icon start size="16" icon="mdi-delete" />
                            Delete
                        </v-btn>
                    </td>
                </tr>
                <tr v-if="sortedUsers.length === 0">
                    <td colspan="5" class="text-center text-medium-emphasis py-8">
                        <v-icon size="large" color="medium-emphasis" icon="mdi-account-off" class="mb-2" />
                        <div>No users found. Create your first user above.</div>
                    </td>
                </tr>
            </tbody>
        </v-table>
        <v-progress-linear v-else indeterminate color="primary" />
    </v-card>

    <!-- Edit dialog -->
    <v-dialog :model-value="!!editing" max-width="480" persistent @update:model-value="editing = null">
        <v-card rounded="xl" elevation="12">
            <v-card-item class="pa-6">
                <v-card-title class="text-h6 font-weight-bold">
                    <v-icon start icon="mdi-pencil" color="primary" />
                    Edit user
                </v-card-title>
            </v-card-item>
            <v-card-text class="pt-0">
                <v-text-field v-model="editUsername" label="Username" variant="outlined" density="comfortable" class="mb-4" />
                <v-text-field v-model="editEmail" label="Email" variant="outlined" density="comfortable" />
            </v-card-text>
            <v-card-actions class="pa-6 pt-2">
                <v-spacer />
                <v-btn variant="text" @click="editing = null">Cancel</v-btn>
                <v-btn color="primary" rounded="lg" @click="updateUser">Save changes</v-btn>
            </v-card-actions>
        </v-card>
    </v-dialog>
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
