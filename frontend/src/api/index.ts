import axios from "axios";
import { userManager } from "../composables/useAuth";

const API_URL = import.meta.env.VITE_API_URL ?? "";

export const api = axios.create({
    baseURL: API_URL || "/",
});

// Attach JWT token to requests
api.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

// --- Tasks API ---
export interface TaskComment {
    id: string;
    authorId: string;
    authorUsername: string;
    content: string;
    createdAtUtc: string;
}

export interface TaskItem {
    id: string;
    userId: string;
    title: string;
    description?: string | null;
    status: "todo" | "in-progress" | "done";
    priority: "low" | "medium" | "high";
    createdAtUtc: string;
    updatedAtUtc?: string | null;
    comments: TaskComment[];
}

export const tasksApi = {
    list: () => api.get<TaskItem[]>("/api/tasks"),
    create: (data: { title: string; description?: string | null; status: string; priority: string }) =>
        api.post<TaskItem>("/api/tasks", data),
    update: (id: string, data: { title: string; description?: string | null; status: string; priority: string }) =>
        api.put<TaskItem>(`/api/tasks/${id}`, data),
    delete: (id: string) => api.delete(`/api/tasks/${id}`),
    addComment: (id: string, content: string) =>
        api.post(`/api/tasks/${id}/comments`, { content }),
};

// --- Orders API ---
export interface OrderItem {
    id: string;
    orderType: "digital" | "physical";
    totalAmount: number;
    status: string;
    paymentMethod: string;
    createdAtUtc: string;
}

export interface OrderTimelineItem {
    key: string;
    label: string;
    state: string;
    occurredAtUtc?: string | null;
    description: string;
}

export interface OrderPaymentDetails {
    orderId: string;
    paymentId: string | null;
    currentAttemptNumber: number;
    orderStatus: string;
    sagaState: string;
    paymentState: string;
    createdAtUtc: string;
    failureReason: string | null;
    events: OrderPaymentEvent[];
}

export interface OrderPaymentEvent {
    attemptNumber: number;
    sequenceNumber: number;
    eventType: string;
    occurredAtUtc: string;
    description: string;
    reason: string | null;
}

export interface OrderWorkflow {
    order: OrderItem;
    payment: OrderPaymentDetails;
    timeline: OrderTimelineItem[];
}

export const ordersApi = {
    list: (asUserId?: string) => {
        const params = asUserId ? { asUserId } : undefined;
        return api.get<OrderItem[]>("/api/orders", { params });
    },
    create: (data: { orderType: string; totalAmount: number; paymentMethod: string }, asUserId?: string) => {
        const params = asUserId ? { asUserId } : undefined;
        return api.post<OrderItem>("/api/orders", data, { params });
    },
    delete: (id: string) => api.delete(`/api/orders/${id}`),
    getWorkflow: (id: string) => api.get<OrderWorkflow>(`/api/orders/${id}/workflow`),
    retryPayment: (id: string) => api.post(`/api/orders/${id}/retry-payment`),
};

// --- Users API ---
export interface AppUser {
    id: string;
    subject: string;
    username: string;
    email?: string | null;
    orders: OrderItem[];
}

export const usersApi = {
    list: () => api.get<AppUser[]>("/api/users"),
    create: (data: { subject: string; username: string; email?: string | null }) =>
        api.post<AppUser>("/api/users", data),
    update: (id: string, data: { username: string; email?: string | null }) =>
        api.put<AppUser>(`/api/users/${id}`, data),
    delete: (id: string) => api.delete(`/api/users/${id}`),
};

// --- Roles API ---
export const rolesApi = {
    list: () => api.get<string[]>("/api/tasks/debugroles"),
};

// --- Order Status Constants ---
export const ORDER_STATUS = {
    Created: "Created",
    PaymentPending: "PaymentPending",
    PaymentAuthorized: "PaymentAuthorized",
    PaymentFailed: "PaymentFailed",
    ExecutionDispatched: "ExecutionDispatched",
    ExecutionStarted: "ExecutionStarted",
    ExecutionCompleted: "ExecutionCompleted",
    ExecutionFailed: "ExecutionFailed",
    Confirmed: "Confirmed",
    Cancelled: "Cancelled",
} as const;
