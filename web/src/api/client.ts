import axios, { AxiosError } from 'axios';
import type {
  CreateCustomerRequest,
  CreateOrderRequest,
  Customer,
  Order,
  OrderStatus,
  PagedResponse,
  UpdateOrderStatusRequest,
} from '../types/api';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  headers: { 'Content-Type': 'application/json' },
});

export class ApiError extends Error {
  constructor(
    message: string,
    public status?: number,
    public details?: string
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

function handleError(error: unknown): never {
  if (error instanceof AxiosError) {
    const data = error.response?.data as { message?: string; details?: string } | undefined;
    throw new ApiError(
      data?.message || error.message,
      error.response?.status,
      data?.details
    );
  }
  throw error;
}

export const customerApi = {
  async search(search: string, page: number, pageSize: number): Promise<PagedResponse<Customer>> {
    try {
      const { data } = await api.get<PagedResponse<Customer>>('/customers', {
        params: { search: search || undefined, page, pageSize },
      });
      return data;
    } catch (e) {
      handleError(e);
    }
  },

  async getById(id: string): Promise<Customer> {
    try {
      const { data } = await api.get<Customer>(`/customers/${id}`);
      return data;
    } catch (e) {
      handleError(e);
    }
  },

  async create(request: CreateCustomerRequest): Promise<Customer> {
    try {
      const { data } = await api.post<Customer>('/customers', request);
      return data;
    } catch (e) {
      handleError(e);
    }
  },
};

export const orderApi = {
  async search(
    customerId: string | undefined,
    status: OrderStatus | undefined,
    page: number,
    pageSize: number,
    sort?: string
  ): Promise<PagedResponse<Order>> {
    try {
      const { data } = await api.get<PagedResponse<Order>>('/orders', {
        params: {
          customerId: customerId || undefined,
          status: status || undefined,
          page,
          pageSize,
          sort,
        },
      });
      return data;
    } catch (e) {
      handleError(e);
    }
  },

  async getById(id: string): Promise<Order> {
    try {
      const { data } = await api.get<Order>(`/orders/${id}`);
      return data;
    } catch (e) {
      handleError(e);
    }
  },

  async create(request: CreateOrderRequest): Promise<Order> {
    try {
      const { data } = await api.post<Order>('/orders', request);
      return data;
    } catch (e) {
      handleError(e);
    }
  },

  async updateStatus(
    id: string,
    request: UpdateOrderStatusRequest,
    idempotencyKey?: string
  ): Promise<Order> {
    try {
      const headers: Record<string, string> = {};
      if (idempotencyKey) headers['Idempotency-Key'] = idempotencyKey;

      const { data } = await api.put<Order>(`/orders/${id}/status`, request, { headers });
      return data;
    } catch (e) {
      handleError(e);
    }
  },
};
