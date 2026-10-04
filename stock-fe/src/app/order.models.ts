export type OrderStatus = 'Pending' | 'Processing' | 'Confirmed' | 'Completed' | 'Cancelled';

export interface Customer {
  id: number;
  name: string;
  email: string | null;
  phone: string | null;
  createdAt: string;
}

export interface CustomerRequest {
  name: string;
  email: string;
  phone: string;
}

export interface OrderItem {
  id: number;
  name: string;
  sku: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface OrderItemRequest {
  name: string;
  sku: string;
  quantity: number;
  unitPrice: number;
}

export interface Order {
  id: number;
  customerId: number | null;
  customerName: string;
  status: OrderStatus;
  createdAt: string;
  updatedAt: string;
  items: OrderItem[];
  total: number;
}

export interface OrderRequest {
  customerId: number;
  status: OrderStatus;
  items: OrderItemRequest[];
}
