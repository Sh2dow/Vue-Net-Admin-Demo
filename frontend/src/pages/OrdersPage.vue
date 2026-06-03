<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRouter, useRoute } from "vue-router";
import { ordersApi, type OrderItem, ORDER_STATUS } from "../api";

const router = useRouter();
const route = useRoute();
const orders = ref<OrderItem[]>([]);
const loading = ref(false);
const showCreateForm = ref(false);

const orderType = ref<"digital" | "physical">("digital");
const totalAmount = ref(100);
const paymentMethod = ref<"credit_card" | "paypal">("credit_card");
const creating = ref(false);

const asUserId = computed(() => (route.query.asUserId ? String(route.query.asUserId) : null));

const statusColor = (s: string) => {
    if (s === ORDER_STATUS.Created) return "info";
    if (s === ORDER_STATUS.PaymentPending) return "warning";
    if (s === ORDER_STATUS.Confirmed) return "success";
    if (s === ORDER_STATUS.Cancelled) return "error";
    return "grey";
};

const statusIcon = (s: string) => {
    if (s === ORDER_STATUS.Created) return "mdi-plus-circle-outline";
    if (s === ORDER_STATUS.PaymentPending) return "mdi-clock-outline";
    if (s === ORDER_STATUS.Confirmed) return "mdi-check-circle-outline";
    if (s === ORDER_STATUS.Cancelled) return "mdi-close-circle-outline";
    return "mdi-help-circle-outline";
};

async function fetchOrders() {
    loading.value = true;
    try {
        const res = await ordersApi.list(asUserId.value ?? undefined);
        orders.value = res.data;
    } finally {
        loading.value = false;
    }
}

async function createOrder() {
    creating.value = true;
    try {
        await ordersApi.create({ orderType: orderType.value, totalAmount: totalAmount.value, paymentMethod: paymentMethod.value }, asUserId.value ?? undefined);
        orderType.value = "digital";
        totalAmount.value = 100;
        paymentMethod.value = "credit_card";
        showCreateForm.value = false;
        await fetchOrders();
    } finally {
        creating.value = false;
    }
}

async function deleteOrder(id: string) {
    await ordersApi.delete(id);
    await fetchOrders();
}

function viewOrder(id: string) {
    router.push({ name: "OrderDetails", params: { id } });
}

onMounted(fetchOrders);
</script>

<template>
    <!-- Header -->
    <div class="d-flex align-center mb-6">
        <div class="d-flex align-center">
            <div class="icon-box mr-3">
                <v-icon size="24" color="white" icon="mdi-cart-outline" />
            </div>
            <div>
                <h1 class="text-h5 font-weight-bold mb-1">Orders</h1>
                <p class="text-body-2 text-medium-emphasis mb-0">
                    <template v-if="asUserId">Showing orders for user ID: <code>{{ asUserId }}</code>
                        <v-btn size="x-small" variant="plain" class="ml-1" @click="router.push({ name: 'Orders' })">Clear filter</v-btn>
                    </template>
                    <template v-else>Manage orders and track their lifecycle</template>
                </p>
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
            {{ showCreateForm ? "Cancel" : "New Order" }}
        </v-btn>
        <v-btn
            icon="mdi-refresh"
            variant="text"
            size="small"
            :loading="loading"
            class="ml-2"
            @click="fetchOrders"
        />
    </div>

    <!-- Create form -->
    <v-expand-transition>
        <v-card v-if="showCreateForm" class="mb-6" elevation="1" rounded="xl">
            <v-card-item class="pa-6">
                <v-card-title class="text-subtitle-1 font-weight-bold mb-4">
                    <v-icon start icon="mdi-cart-plus" color="primary" />
                    Create new order
                </v-card-title>
                <v-row>
                    <v-col cols="12" sm="4">
                        <v-select
                            v-model="orderType"
                            label="Order Type"
                            :items="[
                                { title: 'Digital', value: 'digital' },
                                { title: 'Physical', value: 'physical' },
                            ]"
                            variant="outlined"
                            density="comfortable"
                        />
                    </v-col>
                    <v-col cols="12" sm="3">
                        <v-text-field v-model="totalAmount" label="Total ($)" type="number" variant="outlined" density="comfortable" />
                    </v-col>
                    <v-col cols="12" sm="4">
                        <v-select
                            v-model="paymentMethod"
                            label="Payment Method"
                            :items="[
                                { title: 'Credit Card', value: 'credit_card' },
                                { title: 'PayPal', value: 'paypal' },
                            ]"
                            variant="outlined"
                            density="comfortable"
                        />
                    </v-col>
                    <v-col cols="12" class="text-right">
                        <v-btn color="primary" :loading="creating" rounded="lg" @click="createOrder">
                            <v-icon start icon="mdi-check" />
                            Place order
                        </v-btn>
                    </v-col>
                </v-row>
            </v-card-item>
        </v-card>
    </v-expand-transition>

    <!-- Orders table -->
    <v-card elevation="1" rounded="xl">
        <v-table v-if="!loading" class="text-no-wrap">
            <thead>
                <tr>
                    <th class="text-start text-subtitle-2 font-weight-bold">Order</th>
                    <th class="text-subtitle-2 font-weight-bold">Type</th>
                    <th class="text-subtitle-2 font-weight-bold">Amount</th>
                    <th class="text-subtitle-2 font-weight-bold">Payment</th>
                    <th class="text-subtitle-2 font-weight-bold">Status</th>
                    <th class="text-subtitle-2 font-weight-bold">Actions</th>
                </tr>
            </thead>
            <tbody>
                <tr v-for="order in orders" :key="order.id" class="hover-row">
                    <td>
                        <div class="font-weight-medium">{{ order.id.substring(0, 8) }}...</div>
                        <div class="text-body-2 text-medium-emphasis">{{ new Date(order.createdAtUtc).toLocaleString() }}</div>
                    </td>
                    <td>
                        <v-chip :color="order.orderType === 'digital' ? 'info' : 'secondary'" variant="tonal" size="small">
                            <v-icon start size="16" :icon="order.orderType === 'digital' ? 'mdi-package-variant-closed' : 'mdi-package-variant'" />
                            {{ order.orderType }}
                        </v-chip>
                    </td>
                    <td class="font-weight-medium">${{ Number(order.totalAmount).toFixed(2) }}</td>
                    <td>{{ order.paymentMethod }}</td>
                    <td>
                        <v-chip :color="statusColor(order.status)" variant="tonal" size="small">
                            <v-icon start size="16" :icon="statusIcon(order.status)" />
                            {{ order.status }}
                        </v-chip>
                    </td>
                    <td>
                        <v-btn size="x-small" color="primary" variant="text" class="mr-1" @click="viewOrder(order.id)">
                            <v-icon start size="16" icon="mdi-eye" />
                            Details
                        </v-btn>
                        <v-btn size="x-small" color="error" variant="text" @click="deleteOrder(order.id)">
                            <v-icon start size="16" icon="mdi-delete" />
                            Delete
                        </v-btn>
                    </td>
                </tr>
                <tr v-if="orders.length === 0">
                    <td colspan="6" class="text-center text-medium-emphasis py-8">
                        <v-icon size="large" color="medium-emphasis" icon="mdi-cart-off" class="mb-2" />
                        <div>No orders found. Create your first order above.</div>
                    </td>
                </tr>
            </tbody>
        </v-table>
        <v-progress-linear v-else indeterminate color="primary" />
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
