export type OrderStatus = 'Pending' | 'Paid' | 'Fulfilled' | 'Cancelled';

export interface Customer {
  id: string;
  name: string;
  email: string;
  countryCode: string;
  createdAt: string;
}

export interface CreateCustomerRequest {
  name: string;
  email: string;
  countryCode: string;
}

export interface OrderLineItem {
  id: string;
  productSku: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  customerId: string;
  status: OrderStatus;
  createdAt: string;
  currencyCode: string;
  totalAmount: number;
  rowVersion: string;
  lineItems: OrderLineItem[];
}

export interface CreateOrderLineItemRequest {
  productSku: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateOrderRequest {
  customerId: string;
  currencyCode: string;
  lineItems: CreateOrderLineItemRequest[];
}

export interface UpdateOrderStatusRequest {
  status: OrderStatus;
  rowVersion: string;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ErrorResponse {
  message: string;
  details?: string;
}

export const VALID_TRANSITIONS: Record<OrderStatus, OrderStatus[]> = {
  Pending: ['Paid', 'Cancelled'],
  Paid: ['Fulfilled', 'Cancelled'],
  Fulfilled: [],
  Cancelled: [],
};

export const SADC_COUNTRIES = [
  { code: 'ZA', name: 'South Africa' },
  { code: 'BW', name: 'Botswana' },
  { code: 'ZW', name: 'Zimbabwe' },
  { code: 'NA', name: 'Namibia' },
  { code: 'LS', name: 'Lesotho' },
  { code: 'SZ', name: 'Eswatini' },
  { code: 'MZ', name: 'Mozambique' },
  { code: 'MW', name: 'Malawi' },
  { code: 'ZM', name: 'Zambia' },
];

export const CURRENCIES = ['ZAR', 'BWP', 'ZWL', 'USD', 'NAD', 'LSL', 'SZL', 'MZN', 'MWK', 'ZMW'];
