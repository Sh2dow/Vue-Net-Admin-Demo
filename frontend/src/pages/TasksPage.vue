<script setup lang="ts">
import { ref, onMounted } from "vue";
import { tasksApi, type TaskItem } from "../api";

const tasks = ref<TaskItem[]>([]);
const loading = ref(false);
const creating = ref(false);

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
    <v-card class="mb-4">
        <v-card-item>
            <template #title>
                <v-icon start icon="mdi-checkbox-marked-outline" />
                Task Tracker
            </template>
        </v-card-item>
    </v-card>

    <!-- Create form -->
    <v-card class="mb-4" variant="outlined">
        <v-card-item>
            <v-card-title class="text-subtitle-1 font-weight-bold">Create Task</v-card-title>
            <v-row class="mt-2">
                <v-col cols="12" sm="6">
                    <v-text-field v-model="title" label="Title" placeholder="Prepare release notes" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="12" sm="3">
                    <v-select
                        v-model="status"
                        label="Status"
                        :items="[{ title: 'Todo', value: 'todo' }, { title: 'In Progress', value: 'in-progress' }, { title: 'Done', value: 'done' }]"
                        variant="outlined"
                        density="compact"
                    />
                </v-col>
                <v-col cols="12" sm="3">
                    <v-select
                        v-model="priority"
                        label="Priority"
                        :items="[{ title: 'Low', value: 'low' }, { title: 'Medium', value: 'medium' }, { title: 'High', value: 'high' }]"
                        variant="outlined"
                        density="compact"
                    />
                </v-col>
                <v-col cols="12">
                    <v-textarea v-model="description" label="Description" placeholder="Optional details..." variant="outlined" density="compact" rows="2" />
                </v-col>
                <v-col cols="12" class="text-right">
                    <v-btn color="primary" :loading="creating" @click="createTask">
                        <v-icon start icon="mdi-plus" />
                        Add Task
                    </v-btn>
                </v-col>
            </v-row>
        </v-card-item>
    </v-card>

    <!-- Table -->
    <v-card variant="outlined">
        <v-table v-if="!loading">
            <thead>
                <tr>
                    <th class="text-start">Title</th>
                    <th>Status</th>
                    <th>Priority</th>
                    <th>Comments</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
                <tr v-for="task in tasks" :key="task.id">
                    <td>
                        <div class="font-weight-bold">{{ task.title }}</div>
                        <div class="text-body-2 text-medium-emphasis">{{ task.description || "-" }}</div>
                    </td>
                    <td><v-chip :color="statusColor(task.status)" variant="tonal" size="small">{{ task.status }}</v-chip></td>
                    <td><v-chip :color="task.priority === 'high' ? 'error' : task.priority === 'medium' ? 'warning' : 'success'" variant="outlined" size="small">{{ task.priority }}</v-chip></td>
                    <td>{{ (task.comments ?? []).length }}</td>
                    <td>
                        <v-btn size="x-small" variant="text" class="mr-2" @click="cycleStatus(task)">Cycle Status</v-btn>
                        <v-btn size="x-small" color="error" variant="text" @click="deleteTask(task.id)">Delete</v-btn>
                    </td>
                </tr>
            </tbody>
        </v-table>
        <v-progress-linear v-else indeterminate color="primary" />
    </v-card>

    <!-- Comments per task -->
    <template v-if="!loading">
        <v-card v-for="task in tasks" :key="'comments-' + task.id" class="mt-4" variant="outlined">
            <v-card-item>
                <v-row align="center" no-gutters>
                    <v-col cols="8">
                        <div class="font-weight-bold">{{ task.title }}</div>
                    </v-col>
                    <v-col cols="4" class="text-right">
                        <v-chip size="small" variant="tonal">{{ (task.comments ?? []).length }} comments</v-chip>
                    </v-col>
                </v-row>

                <v-divider class="my-2" />

                <div v-if="(task.comments ?? []).length === 0" class="text-body-2 text-medium-emphasis pa-2">
                    No comments yet.
                </div>
                <div v-else class="pa-2">
                    <div v-for="comment in task.comments ?? []" :key="comment.id" class="mb-2">
                        <strong>{{ comment.authorUsername }}</strong>: {{ comment.content }}
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
                        <v-btn size="small" color="primary" @click="addComment(task.id)">Post</v-btn>
                    </v-col>
                </v-row>
            </v-card-item>
        </v-card>
    </template>
</template>
