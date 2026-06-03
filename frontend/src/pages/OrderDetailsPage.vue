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
    <!-- Breadcrumb -->
    <div class="mb-4">
        <v-breadcrumbs class="mb-2 pl-0" :items="[{ title: 'Orders', disabled: false }, { title: orderId.substring(0, 8), disabled: true }]" divider="/">
            <template #item="{ item }">
                <v-breadcrumbs-item :disabled="item.disabled" @click="router.push({ name: 'Orders' })">{{ item.title }}</v-breadcrumbs-item>
            </template>
        </v-breadcrumbs>

        <!-- Header -->
        <div class="d-flex align-center mb-4">
            <div class="d-flex align-center">
                <div class="icon-box mr-3">
                    <v-icon size="24" color="white" icon="mdi-cart-outline" />
                </div>
                <div>
                    <h1 class="text-h5 font-weight-bold mb-1">Order Details</h1>
                    <p class="text-body-2 text-medium-emphasis mb-0 font-mono">{{ orderId }}</p>
                </div>
            </div>
        </div>
    </div>

    <!-- Order info card -->
    <v-card v-if="workflow && !loading" class="mb-6" elevation="1" rounded="xl">
        <v-card-item class="pa-6">
            <v-row>
                <v-col cols="12" sm="4">
                    <div class="text-subtitle-2 text-medium-emphasis mb-1">Order Type</div>
                    <v-chip :color="workflow.order.orderType === 'digital' ? 'info' : 'secondary'" variant="tonal" size="small">
                        <v-icon start size="16" :icon="workflow.order.orderType === 'digital' ? 'mdi-package-variant-closed' : 'mdi-package-variant'" />
                        {{ workflow.order.orderType }}
                    </v-chip>
                </v-col>
                <v-col cols="12" sm="4">
                    <div class="text-subtitle-2 text-medium-emphasis mb-1">Total Amount</div>
                    <div class="text-h6 font-weight-bold">${{ Number(workflow.order.totalAmount).toFixed(2) }}</div>
                </v-col>
                <v-col cols="12" sm="4">
                    <div class="text-subtitle-2 text-medium-emphasis mb-1">Status</div>
                    <v-chip color="primary" variant="tonal" size="small">
                        {{ workflow.order.status }}
                    </v-chip>
                </v-col>
                <v-col cols="12">
                    <v-divider class="my-2" />
                </v-col>
                <v-col cols="12" sm="6">
                    <div class="text-subtitle-2 text-medium-emphasis mb-1">Payment State</div>
                    <div class="text-body-1">{{ workflow.payment?.paymentState ?? "—" }}</div>
                </v-col>
                <v-col cols="12" sm="6">
                    <div class="text-subtitle-2 text-medium-emphasis mb-1">Created</div>
                    <div class="text-body-1">{{ new Date(workflow.order.createdAtUtc).toLocaleString() }}</div>
                </v-col>
            </v-row>
        </v-card-item>
    </v-card>

    <!-- Timeline -->
    <v-card v-if="workflow?.timeline && !loading" elevation="1" rounded="xl">
        <v-card-item class="pa-6">
            <v-card-title class="text-subtitle-1 font-weight-bold mb-4">
                <v-icon start icon="mdi-timer-sand" color="primary" />
                Saga Timeline
            </v-card-title>
            <v-timeline side="end" truncate-line="both">
                <v-timeline-item
                    v-for="step in workflow.timeline"
                    :key="step.key"
                    :dot-color="stepColor(step.state)"
                    size="small"
                    filled-dot
                    fill-dot
                >
                    <v-card variant="flat" :color="stepColor(step.state)" class="pa-4" rounded="lg">
                        <div class="d-flex align-center justify-space-between mb-2">
                            <div class="d-flex align-center">
                                <v-icon :icon="stepIcon(step.state)" :color="stepColor(step.state)" size="small" class="mr-2" />
                                <span class="font-weight-bold">{{ step.label }}</span>
                            </div>
                            <v-chip :color="stepColor(step.state)" variant="tonal" size="x-small">
                                {{ step.state }}
                            </v-chip>
                        </div>
                        <div class="text-body-2 text-medium-emphasis">
                            {{ step.description }}
                            <span v-if="step.occurredAtUtc" class="ml-1">— {{ new Date(step.occurredAtUtc).toLocaleString() }}</span>
                        </div>
                        <div v-if="step.key === 'payment-failed' && step.state === 'Completed'" class="mt-3">
                            <v-btn
                                color="error"
                                size="small"
                                :loading="retrying"
                                prepend-icon="mdi-replay"
                                variant="tonal"
                                rounded="lg"
                                @click="retryPayment"
                            >
                                Retry Payment
                            </v-btn>
                        </div>
                    </v-card>
                </v-timeline-item>
            </v-timeline>
        </v-card-item>
    </v-card>

    <!-- Loading state -->
    <v-card v-else class="mt-6" elevation="1" rounded="xl">
        <v-card-item class="pa-8 text-center">
            <v-progress-circular indeterminate color="primary" size="48" />
            <div class="text-body-1 mt-4 text-medium-emphasis">Loading order workflow...</div>
        </v-card-item>
    </v-card>

    <!-- Back button -->
    <v-btn
        class="mt-6"
        color="primary"
        variant="text"
        prepend-icon="mdi-arrow-left"
        rounded="lg"
        @click="router.push({ name: 'Orders' })"
    >
        Back to Orders
    </v-btn>
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
</style>
