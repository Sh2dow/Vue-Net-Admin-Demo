<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRouter, useRoute } from "vue-router";
import { ordersApi, type OrderItem, ORDER_STATUS } from "../api";

const router = useRouter();
const route = useRoute();
const orders = ref<OrderItem[]>([]);
const loading = ref(false);

const orderType = ref<"product" | "subscription" | "coupon">("product");
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
        orderType.value = "product";
        totalAmount.value = 100;
        paymentMethod.value = "credit_card";
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
    <v-card class="mb-4">
        <v-card-item>
            <template #title>
                <v-icon start icon="mdi-cart-outline" />
                Orders
            </template>
            <template v-if="asUserId" #subtitle>
                Showing orders for user ID: <code>{{ asUserId }}</code>
                <v-btn size="x-small" variant="text" class="ml-2" @click="router.push({ name: 'Orders' })">Clear filter</v-btn>
            </template>
        </v-card-item>
    </v-card>

    <!-- Create form -->
    <v-card class="mb-4" variant="outlined">
        <v-card-item>
            <v-card-title class="text-subtitle-1 font-weight-bold">Create Order</v-card-title>
            <v-row class="mt-2">
                <v-col cols="12" sm="4">
                    <v-select
                        v-model="orderType"
                        label="Order Type"
                        :items="[
                            { title: 'Product', value: 'product' },
                            { title: 'Subscription', value: 'subscription' },
                            { title: 'Coupon', value: 'coupon' },
                        ]"
                        variant="outlined"
                        density="compact"
                    />
                </v-col>
                <v-col cols="12" sm="3">
                    <v-text-field v-model="totalAmount" label="Total ($)" type="number" variant="outlined" density="compact" />
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
                        density="compact"
                    />
                </v-col>
                <v-col cols="12" sm="1" class="d-flex align-center">
                    <v-btn color="primary" :loading="creating" @click="createOrder">
                        <v-icon icon="mdi-plus" />
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
                    <th class="text-start">Order</th>
                    <th>Type</th>
                    <th>Amount</th>
                    <th>Payment</th>
                    <th>Status</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
                <tr v-for="order in orders" :key="order.id">
                    <td>
                        <div class="font-weight-bold">{{ order.id.substring(0, 8) }}...</div>
                        <div class="text-body-2 text-medium-emphasis">{{ new Date(order.createdAtUtc).toLocaleString() }}</div>
                    </td>
                    <td>{{ order.orderType }}</td>
                    <td>${{ Number(order.totalAmount).toFixed(2) }}</td>
                    <td>{{ order.paymentMethod }}</td>
                    <td>
                        <v-chip :color="statusColor(order.status)" variant="tonal" size="small">
                            {{ order.status }}
                        </v-chip>
                    </td>
                    <td>
                        <v-btn size="x-small" color="primary" variant="text" class="mr-1" @click="viewOrder(order.id)">Details</v-btn>
                        <v-btn size="x-small" color="error" variant="text" @click="deleteOrder(order.id)">Delete</v-btn>
                    </td>
                </tr>
                <tr v-if="orders.length === 0">
                    <td colspan="6" class="text-center text-medium-emphasis pa-8">No orders found.</td>
                </tr>
            </tbody>
        </v-table>
        <v-progress-linear v-else indeterminate color="primary" />
    </v-card>
</template>
