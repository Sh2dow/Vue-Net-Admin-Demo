<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ordersApi, type OrderWorkflow } from "../api";

const route = useRoute();
const router = useRouter();
const workflow = ref<OrderWorkflow | null>(null);
const loading = ref(false);
const retrying = ref(false);
let pollTimer: ReturnType<typeof setTimeout> | null = null;

const orderId = String(route.params.id);

const stepIcon = (state: string) => {
    if (state === "Completed") return "mdi-check-circle";
    if (state === "Failed") return "mdi-alert-circle";
    if (state === "Pending") return "mdi-clock-outline";
    return "mdi-circle-slice-8";
};

const stepColor = (state: string) => {
    if (state === "Completed") return "success";
    if (state === "Failed") return "error";
    if (state === "Pending") return "warning";
    return "grey";
};

function stopPolling() {
    if (pollTimer) {
        clearTimeout(pollTimer);
        pollTimer = null;
    }
}

async function fetchWorkflow(isInitialFetch: boolean = false) {
    if (isInitialFetch) {
        loading.value = true;
    }
    try {
        const res = await ordersApi.getWorkflow(orderId);
        workflow.value = res.data;
        stopPolling();

        // Only poll while payment is still pending
        if (res.data.payment?.paymentState === "PaymentPending") {
            pollTimer = setTimeout(fetchWorkflow, 3000);
        }
    } catch (err) {
        console.error("Failed to fetch order workflow:", err);
        stopPolling();
    } finally {
        if (isInitialFetch) {
            loading.value = false;
        }
    }
}

async function retryPayment() {
    retrying.value = true;
    try {
        await ordersApi.retryPayment(orderId);
        await fetchWorkflow(false);
    } finally {
        retrying.value = false;
    }
}

onUnmounted(stopPolling);
onMounted(() => fetchWorkflow(true));
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
                    <div>Order Type: {{ workflow.order.orderType }}</div>
                    <div>Total: ${{ Number(workflow.order.totalAmount).toFixed(2) }}</div>
                    <div>Status: <strong>{{ workflow.order.status }}</strong></div>
                </template>
            </v-card-item>
        </v-card>

        <!-- Timeline -->
        <v-card v-if="workflow?.timeline && !loading" variant="outlined">
            <v-card-item>
                <v-card-title class="text-subtitle-1 font-weight-bold">Saga Timeline</v-card-title>
                <v-timeline side="end">
                    <v-timeline-item
                        v-for="step in workflow.timeline"
                        :key="step.key"
                        :dot-color="stepColor(step.state)"
                        size="small"
                        filled-dot
                    >
                        <v-card variant="tonal" :color="stepColor(step.state)">
                            <v-card-item>
                                <v-icon :icon="stepIcon(step.state)" :color="stepColor(step.state)" start />
                                <div>
                                    <div class="font-weight-bold">{{ step.label }}</div>
                                    <div class="text-body-2 text-medium-emphasis">
                                        {{ step.description }}
                                        <span v-if="step.occurredAtUtc"> — {{ new Date(step.occurredAtUtc).toLocaleString() }}</span>
                                    </div>
                                </div>
                                <v-chip :color="stepColor(step.state)" variant="tonal" size="small" class="ml-auto">
                                    {{ step.state }}
                                </v-chip>
                            </v-card-item>
                            <v-card-actions v-if="step.key === 'payment-failed' && step.state === 'Completed'">
                                <v-btn
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
