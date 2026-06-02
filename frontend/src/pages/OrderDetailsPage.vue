<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ordersApi, type OrderWorkflow } from "../api";

const route = useRoute();
const router = useRouter();
const workflow = ref<OrderWorkflow | null>(null);
const loading = ref(false);
const retrying = ref(false);
let pollTimer: number | null = null;

const orderId = String(route.params.id);

const stepIcon = (state: string) => {
    if (state === "Completed") return "mdi-check-circle";
    if (state === "Failed") return "mdi-alert-circle";
    if (state === "In Progress") return "mdi-clock-outline";
    return "mdi-circle-slice-8";
};

const stepColor = (state: string) => {
    if (state === "Completed") return "success";
    if (state === "Failed") return "error";
    if (state === "In Progress") return "warning";
    return "grey";
};

async function fetchWorkflow() {
    loading.value = true;
    try {
        const res = await ordersApi.getWorkflow(orderId);
        workflow.value = res.data;

        // If PaymentPending, start polling
        if (pollTimer) clearInterval(pollTimer);
        if (res.data.steps?.some((s) => s.stepName === "Payment" && s.stepState === "In Progress")) {
            pollTimer = window.setInterval(fetchWorkflow, 3000);
        } else {
            pollTimer = null;
        }
    } finally {
        loading.value = false;
    }
}

async function retryPayment() {
    retrying.value = true;
    try {
        await ordersApi.retryPayment(orderId);
        await fetchWorkflow();
    } finally {
        retrying.value = false;
    }
}

onUnmounted(() => {
    if (pollTimer) clearInterval(pollTimer);
});

onMounted(fetchWorkflow);
</script>

<template>
    <div>
        <v-card class="mb-4">
            <v-card-item>
                <v-breadcrumbs class="mb-2" :items="[{ title: 'Orders', disabled: false }, { title: orderId.substring(0, 8), disabled: true }]" divider="/">
                    <template #item="{ item }">
                        <v-breadcrumbs-item :disabled="item.disabled" @click="router.push({ name: 'Orders' })">{{ item.title }}</v-breadcrumbs-item>
                    </template>
                </v-breadcrumbs>
                <template #title>
                    <v-icon start icon="mdi-cart-outline" />
                    Order Workflow Details
                </template>
                <template v-if="workflow" #subtitle>
                    <div>Order Type: {{ workflow.orderType }}</div>
                    <div>Total: ${{ Number(workflow.totalAmount).toFixed(2) }}</div>
                </template>
            </v-card-item>
        </v-card>

        <!-- Timeline -->
        <v-card v-if="workflow?.steps && !loading" variant="outlined">
            <v-card-item>
                <v-card-title class="text-subtitle-1 font-weight-bold">Saga Timeline</v-card-title>
                <v-timeline side="end">
                    <v-timeline-item
                        v-for="step in workflow.steps"
                        :key="step.stepName"
                        :dot-color="stepColor(step.stepState)"
                        size="small"
                        filled-dot
                    >
                        <v-card variant="tonal" :color="stepColor(step.stepState)">
                            <v-card-item>
                                <v-icon :icon="stepIcon(step.stepState)" :color="stepColor(step.stepState)" start />
                                <div>
                                    <div class="font-weight-bold">{{ step.stepName }}</div>
                                    <div class="text-body-2 text-medium-emphasis">
                                        {{ step.stepState }}
                                        <span v-if="step.error"> — {{ step.error }}</span>
                                    </div>
                                </div>
                                <v-chip :color="stepColor(step.stepState)" variant="tonal" size="small" class="ml-auto">
                                    {{ step.stepState }}
                                </v-chip>
                            </v-card-item>
                            <v-card-actions>
                                <v-btn
                                    v-if="step.stepName === 'Payment' && step.stepState === 'Failed'"
                                    color="error"
                                    size="x-small"
                                    :loading="retrying"
                                    prepend-icon="mdi-replay"
                                    @click="retryPayment"
                                >
                                    Retry Payment
                                </v-btn>
                            </v-card-actions>
                        </v-card>
                    </v-timeline-item>
                </v-timeline>
            </v-card-item>
        </v-card>

        <v-progress-linear v-else indeterminate color="primary" />

        <v-card class="mt-4" variant="outlined">
            <v-card-item>
                <v-btn color="primary" prepend-icon="mdi-arrow-left" @click="router.push({ name: 'Orders' })">Back to Orders</v-btn>
            </v-card-item>
        </v-card>
    </div>
</template>
