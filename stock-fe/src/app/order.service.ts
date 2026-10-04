import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Customer, CustomerRequest, Order, OrderRequest } from './order.models';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly apiUrl = 'http://localhost:5158/api';

  constructor(private readonly http: HttpClient) {}

  getCustomers() {
    return this.http.get<Customer[]>(`${this.apiUrl}/customers`);
  }

  createCustomer(request: CustomerRequest) {
    return this.http.post<Customer>(`${this.apiUrl}/customers`, request);
  }

  updateCustomer(id: number, request: CustomerRequest) {
    return this.http.put<Customer>(`${this.apiUrl}/customers/${id}`, request);
  }

  deleteCustomer(id: number) {
    return this.http.delete<void>(`${this.apiUrl}/customers/${id}`);
  }

  getOrders() {
    return this.http.get<Order[]>(`${this.apiUrl}/orders`);
  }

  createOrder(request: OrderRequest) {
    return this.http.post<Order>(`${this.apiUrl}/orders`, request);
  }

  updateOrder(id: number, request: OrderRequest) {
    return this.http.put<Order>(`${this.apiUrl}/orders/${id}`, request);
  }

  deleteOrder(id: number) {
    return this.http.delete<void>(`${this.apiUrl}/orders/${id}`);
  }
}
