<script setup lang="ts">
import { ref, onMounted } from "vue";
import { tasksApi, type TaskItem } from "../api";

const tasks = ref<TaskItem[]>([]);
const loading = ref(false);
const creating = ref(false);
const showCreateForm = ref(false);

const title = ref("");
const description = ref("");
const status = ref<"todo" | "in-progress" | "done">("todo");
const priority = ref<"low" | "medium" | "high">("medium");
const commentDrafts = ref<Record<string, string>>({});

const statusColor = (s: string) => {
    if (s === "done") return "success";
    if (s === "in-progress") return "warning";
    return "grey";
};

const statusIcon = (s: string) => {
    if (s === "done") return "mdi-check-circle";
    if (s === "in-progress") return "mdi-timer-sand";
    return "mdi-help-circle";
};

const priorityColor = (p: string) => {
    if (p === "high") return "error";
    if (p === "medium") return "warning";
    return "success";
};

async function fetchTasks() {
    loading.value = true;
    try {
        const res = await tasksApi.list();
        tasks.value = res.data;
    } finally {
        loading.value = false;
    }
}

async function createTask() {
    if (!title.value.trim()) return;
    creating.value = true;
    try {
        await tasksApi.create({ title: title.value.trim(), description: description.value.trim() || null, status: status.value, priority: priority.value });
        title.value = "";
        description.value = "";
        status.value = "todo";
        priority.value = "medium";
        showCreateForm.value = false;
        await fetchTasks();
    } finally {
        creating.value = false;
    }
}

async function cycleStatus(task: TaskItem) {
    const next = task.status === "todo" ? "in-progress" : task.status === "in-progress" ? "done" : "todo";
    await tasksApi.update(task.id, { title: task.title, description: task.description, status: next, priority: task.priority });
    await fetchTasks();
}

async function deleteTask(id: string) {
    await tasksApi.delete(id);
    await fetchTasks();
}

async function addComment(taskId: string) {
    const content = commentDrafts.value[taskId]?.trim();
    if (!content) return;
    await tasksApi.addComment(taskId, content);
    commentDrafts.value = { ...commentDrafts.value, [taskId]: "" };
    await fetchTasks();
}

onMounted(fetchTasks);
</script>

<template>
    <!-- Header -->
    <div class="d-flex align-center mb-6">
        <div class="d-flex align-center">
            <div class="icon-box mr-3">
                <v-icon size="24" color="white" icon="mdi-checkbox-marked-outline" />
            </div>
            <div>
                <h1 class="text-h5 font-weight-bold mb-1">Task Tracker</h1>
                <p class="text-body-2 text-medium-emphasis mb-0">Manage tasks and track progress</p>
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
            {{ showCreateForm ? "Cancel" : "New Task" }}
        </v-btn>
        <v-btn
            icon="mdi-refresh"
            variant="text"
            size="small"
            :loading="loading"
            class="ml-2"
            @click="fetchTasks"
        />
    </div>

    <!-- Create form -->
    <v-expand-transition>
        <v-card v-if="showCreateForm" class="mb-6" elevation="1" rounded="xl">
            <v-card-item class="pa-6">
                <v-card-title class="text-subtitle-1 font-weight-bold mb-4">
                    <v-icon start icon="mdi-plus-circle" color="primary" />
                    Create new task
                </v-card-title>
                <v-row>
                    <v-col cols="12" sm="6">
                        <v-text-field v-model="title" label="Title" placeholder="Prepare release notes" variant="outlined" density="comfortable" />
                    </v-col>
                    <v-col cols="12" sm="3">
                        <v-select
                            v-model="status"
                            label="Status"
                            :items="[{ title: 'Todo', value: 'todo' }, { title: 'In Progress', value: 'in-progress' }, { title: 'Done', value: 'done' }]"
                            variant="outlined"
                            density="comfortable"
                        />
                    </v-col>
                    <v-col cols="12" sm="3">
                        <v-select
                            v-model="priority"
                            label="Priority"
                            :items="[{ title: 'Low', value: 'low' }, { title: 'Medium', value: 'medium' }, { title: 'High', value: 'high' }]"
                            variant="outlined"
                            density="comfortable"
                        />
                    </v-col>
                    <v-col cols="12">
                        <v-textarea v-model="description" label="Description" placeholder="Optional details..." variant="outlined" density="comfortable" rows="2" />
                    </v-col>
                    <v-col cols="12" class="text-right">
                        <v-btn color="primary" :loading="creating" rounded="lg" @click="createTask">
                            <v-icon start icon="mdi-check" />
                            Add task
                        </v-btn>
                    </v-col>
                </v-row>
            </v-card-item>
        </v-card>
    </v-expand-transition>

    <!-- Tasks table -->
    <v-card elevation="1" rounded="xl">
        <v-table v-if="!loading" class="text-no-wrap">
            <thead>
                <tr>
                    <th class="text-start text-subtitle-2 font-weight-bold">Task</th>
                    <th class="text-subtitle-2 font-weight-bold">Status</th>
                    <th class="text-subtitle-2 font-weight-bold">Priority</th>
                    <th class="text-subtitle-2 font-weight-bold">Comments</th>
                    <th class="text-subtitle-2 font-weight-bold">Actions</th>
                </tr>
            </thead>
            <tbody>
                <tr v-for="task in tasks" :key="task.id" class="hover-row">
                    <td>
                        <div class="font-weight-medium">{{ task.title }}</div>
                        <div class="text-body-2 text-medium-emphasis">{{ task.description || "—" }}</div>
                    </td>
                    <td>
                        <v-chip :color="statusColor(task.status)" variant="tonal" size="small">
                            <v-icon start size="16" :icon="statusIcon(task.status)" />
                            {{ task.status }}
                        </v-chip>
                    </td>
                    <td>
                        <v-chip :color="priorityColor(task.priority)" variant="outlined" size="small">
                            {{ task.priority }}
                        </v-chip>
                    </td>
                    <td>
                        <v-chip v-if="task.comments?.length" size="small" color="info" variant="tonal">
                            {{ task.comments.length }}
                        </v-chip>
                        <span v-else class="text-medium-emphasis">—</span>
                    </td>
                    <td>
                        <v-btn size="x-small" color="primary" variant="text" class="mr-1" @click="cycleStatus(task)">
                            <v-icon start size="16" icon="mdi-sync" />
                            Status
                        </v-btn>
                        <v-btn size="x-small" color="error" variant="text" @click="deleteTask(task.id)">
                            <v-icon start size="16" icon="mdi-delete" />
                            Delete
                        </v-btn>
                    </td>
                </tr>
                <tr v-if="tasks.length === 0">
                    <td colspan="5" class="text-center text-medium-emphasis py-8">
                        <v-icon size="large" color="medium-emphasis" icon="mdi-checkbox-blank-outline" class="mb-2" />
                        <div>No tasks found. Create your first task above.</div>
                    </td>
                </tr>
            </tbody>
        </v-table>
        <v-progress-linear v-else indeterminate color="primary" />
    </v-card>

    <!-- Comments per task -->
    <template v-if="!loading">
        <v-card v-for="task in tasks" :key="'comments-' + task.id" class="mt-4" elevation="1" rounded="xl">
            <v-card-item class="pa-6">
                <div class="d-flex align-center mb-2">
                    <v-icon start color="primary" size="small" icon="mdi-comment-text" />
                    <span class="font-weight-medium">{{ task.title }}</span>
                    <v-chip size="x-small" color="info" variant="tonal" class="ml-2">
                        {{ (task.comments ?? []).length }} comments
                    </v-chip>
                </div>

                <v-divider class="mb-2" />

                <div v-if="(task.comments ?? []).length === 0" class="text-body-2 text-medium-emphasis pa-2">
                    No comments yet.
                </div>
                <div v-else class="pa-2">
                    <div v-for="comment in task.comments ?? []" :key="comment.id" class="mb-2 d-flex align-start">
                        <v-avatar size="24" color="primary" variant="tonal" class="mr-2 mt-1">
                            <span class="text-caption font-weight-bold">{{ comment.authorUsername.charAt(0).toUpperCase() }}</span>
                        </v-avatar>
                        <div>
                            <span class="font-weight-medium text-body-2">{{ comment.authorUsername }}</span>
                            <span class="text-body-2 ml-1">{{ comment.content }}</span>
                        </div>
                    </div>
                </div>

                <v-row align="center" no-gutters class="mt-2">
                    <v-col cols="10">
                        <v-text-field
                            v-model="commentDrafts[task.id]"
                            label="New Comment"
                            placeholder="Add a comment..."
                            variant="outlined"
                            density="compact"
                            hide-details
                        />
                    </v-col>
                    <v-col cols="2" class="text-right">
                        <v-btn size="small" color="primary" rounded="lg" @click="addComment(task.id)">
                            <v-icon icon="mdi-send" />
                        </v-btn>
                    </v-col>
                </v-row>
            </v-card-item>
        </v-card>
    </template>
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
