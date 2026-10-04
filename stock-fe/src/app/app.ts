import { CurrencyPipe, DatePipe, isPlatformBrowser } from '@angular/common';
import { ChangeDetectorRef, Component, Inject, OnInit, PLATFORM_ID } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Customer, CustomerRequest, Order, OrderRequest, OrderStatus } from './order.models';
import { OrderService } from './order.service';

type PendingDeletion =
  | { kind: 'order'; order: Order }
  | { kind: 'customer'; customer: Customer };

@Component({
  imports: [FormsModule, DatePipe, CurrencyPipe],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App implements OnInit {
  readonly statuses: OrderStatus[] = ['Pending', 'Processing', 'Confirmed', 'Completed', 'Cancelled'];
  readonly statusFilters = ['All orders', ...this.statuses];
  private readonly statusTransitions: Record<OrderStatus, OrderStatus[]> = {
    Pending: ['Processing', 'Cancelled'],
    Processing: ['Confirmed', 'Cancelled'],
    Confirmed: ['Completed', 'Cancelled'],
    Completed: [],
    Cancelled: [],
  };
  readonly today = new Date();
  customers: Customer[] = [];
  orders: Order[] = [];
  selectedFilter = 'All orders';
  searchTerm = '';
  loading = true;
  savingOrder = false;
  savingCustomer = false;
  errorMessage = '';
  orderSaveToast = '';
  successMessage = '';
  orderDialogOpen = false;
  customerDialogOpen = false;
  customerFormOpen = false;
  pendingDeletion: PendingDeletion | null = null;
  deletingRecord = false;
  openOrderActionsId: number | null = null;
  readOnlyOrder = false;
  orderCustomerName = '';
  editingOrderId: number | null = null;
  editingOrderStatus: OrderStatus | null = null;
  editingCustomerId: number | null = null;
  orderDraft: OrderRequest = this.emptyOrderDraft();
  customerDraft: CustomerRequest = this.emptyCustomerDraft();

  constructor(
    private readonly orderService: OrderService,
    private readonly changeDetector: ChangeDetectorRef,
    @Inject(PLATFORM_ID) private readonly platformId: object,
  ) {}

  get activeOrders(): Order[] {
    return this.orders.filter((order) =>
      order.status === 'Pending' || order.status === 'Processing' || order.status === 'Confirmed',
    );
  }

  get filteredOrders(): Order[] {
    const query = this.searchTerm.trim().toLowerCase();
    return this.orders.filter((order) => {
      const matchesStatus = this.selectedFilter === 'All orders' || order.status === this.selectedFilter;
      const matchesQuery = !query || [
        String(order.id), order.customerName, ...order.items.map((item) => item.name),
      ].some((value) => value.toLowerCase().includes(query));
      return matchesStatus && matchesQuery;
    });
  }

  get draftTotal(): number {
    return this.orderDraft.items.reduce(
      (total, item) => total + (Number(item.quantity) || 0) * (Number(item.unitPrice) || 0),
      0,
    );
  }

  get ordersBlockingCustomerDelete(): Order[] {
    const deletion = this.pendingDeletion;
    if (!deletion || deletion.kind !== 'customer') {
      return [];
    }
    return this.orders.filter((order) =>
      order.customerId === deletion.customer.id && order.status !== 'Completed',
    );
  }

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      void this.loadData();
    }
  }

  async loadData(): Promise<void> {
    this.loading = true;
    this.errorMessage = '';
    try {
      const [customers, orders] = await Promise.all([
        firstValueFrom(this.orderService.getCustomers()),
        firstValueFrom(this.orderService.getOrders()),
      ]);
      this.customers = customers;
      this.orders = orders;
    } catch (error) {
      this.errorMessage = this.errorText(error, 'Could not load the order queue.');
    } finally {
      this.loading = false;
      this.changeDetector.markForCheck();
    }
  }

  countByStatus(status: string): number {
    return this.orders.filter((order) => order.status === status).length;
  }

  openNewOrder(): void {
    this.clearNotices();
    this.openOrderActionsId = null;
    this.readOnlyOrder = false;
    this.orderCustomerName = '';
    this.editingOrderId = null;
    this.editingOrderStatus = null;
    this.orderDraft = this.emptyOrderDraft();
    this.orderDialogOpen = true;
  }

  editOrder(order: Order): void {
    this.clearNotices();
    this.openOrderActionsId = null;
    this.readOnlyOrder = false;
    this.orderCustomerName = order.customerName;
    this.setOrderDraft(order);
    this.orderDialogOpen = true;
  }

  viewOrder(order: Order): void {
    this.clearNotices();
    this.openOrderActionsId = null;
    this.readOnlyOrder = true;
    this.orderCustomerName = order.customerName;
    this.setOrderDraft(order);
    this.orderDialogOpen = true;
  }

  toggleOrderActions(orderId: number): void {
    this.openOrderActionsId = this.openOrderActionsId === orderId ? null : orderId;
  }

  private setOrderDraft(order: Order): void {
    this.editingOrderId = order.id;
    this.editingOrderStatus = order.status;
    this.orderDraft = {
      customerId: order.customerId ?? 0,
      status: order.status,
      items: order.items.map((item) => ({
        name: item.name,
        sku: item.sku ?? '',
        quantity: item.quantity,
        unitPrice: item.unitPrice,
      })),
    };
  }

  closeOrderDialog(): void {
    this.orderDialogOpen = false;
    this.errorMessage = '';
    this.orderSaveToast = '';
  }

  addLineItem(): void {
    this.orderDraft.items.push({ name: '', sku: '', quantity: 1, unitPrice: 0 });
  }

  removeLineItem(index: number): void {
    if (this.orderDraft.items.length > 1) {
      this.orderDraft.items.splice(index, 1);
    }
  }

  async saveOrder(): Promise<void> {
    if (this.readOnlyOrder) {
      return;
    }
    this.clearNotices();
    if (!this.orderDraft.customerId || this.orderDraft.items.some((item) =>
      !item.name.trim() || Number(item.unitPrice) < 0,
    )) {
      this.errorMessage = 'Select a customer and enter a name and non-negative unit price for every item.';
      return;
    }
    if (this.orderDraft.items.some((item) =>
      !Number.isInteger(Number(item.quantity)) || Number(item.quantity) < 1 || Number(item.quantity) > 2_147_483_647,
    )) {
      this.errorMessage = 'Each item quantity must be a positive whole number.';
      return;
    }
    const skus = this.orderDraft.items
      .map((item) => item.sku.trim())
      .filter(Boolean);
    if (new Set(skus.map((sku) => sku.toLocaleUpperCase())).size !== skus.length) {
      this.errorMessage = 'Each non-empty SKU can only appear once in an order.';
      return;
    }
    this.savingOrder = true;
    try {
      if (this.editingOrderId === null) {
        await firstValueFrom(this.orderService.createOrder(this.orderDraft));
        this.successMessage = 'Order created.';
      } else {
        await firstValueFrom(this.orderService.updateOrder(this.editingOrderId, this.orderDraft));
        this.successMessage = 'Order updated.';
      }
      this.closeOrderDialog();
      this.changeDetector.markForCheck();
      void this.loadData();
    } catch (error) {
      this.orderSaveToast =
        `${this.errorText(error, 'Could not save the order.')} Please review or edit the order and try again.`;
      this.changeDetector.markForCheck();
    } finally {
      this.savingOrder = false;
      this.changeDetector.markForCheck();
    }
  }

  dismissOrderSaveToast(): void {
    this.orderSaveToast = '';
    this.changeDetector.markForCheck();
  }

  async changeStatus(order: Order, status: OrderStatus): Promise<void> {
    if (status === order.status) {
      return;
    }
    if (order.customerId === null) {
      this.errorMessage = 'This completed order has no linked customer. Edit it and select a customer before changing its status.';
      return;
    }
    if (!this.statusTransitions[order.status].includes(status)) {
      this.errorMessage = `An order in ${order.status} status cannot move to ${status}.`;
      return;
    }
    const request: OrderRequest = {
      customerId: order.customerId,
      status,
      items: order.items.map(({ name, sku, quantity, unitPrice }) => ({
        name,
        sku: sku ?? '',
        quantity,
        unitPrice,
      })),
    };
    try {
      await firstValueFrom(this.orderService.updateOrder(order.id, request));
      this.successMessage = `Order #${order.id} is now ${status.toLowerCase()}.`;
      await this.loadData();
    } catch (error) {
      this.errorMessage = this.errorText(error, 'Could not update the order status.');
      await this.loadData();
    }
  }

  async deleteOrder(order: Order): Promise<void> {
    this.openOrderActionsId = null;
    this.clearNotices();
    this.pendingDeletion = { kind: 'order', order };
  }

  openCustomers(): void {
    this.clearNotices();
    this.customerDialogOpen = true;
    this.customerFormOpen = false;
    this.editingCustomerId = null;
  }

  closeCustomerDialog(): void {
    this.customerDialogOpen = false;
    this.customerFormOpen = false;
  }

  startNewCustomer(): void {
    this.editingCustomerId = null;
    this.customerDraft = this.emptyCustomerDraft();
    this.customerFormOpen = true;
  }

  editCustomer(customer: Customer): void {
    this.editingCustomerId = customer.id;
    this.customerDraft = { name: customer.name, email: customer.email ?? '', phone: customer.phone ?? '' };
    this.customerFormOpen = true;
  }

  cancelCustomerEdit(): void {
    this.customerFormOpen = false;
    this.editingCustomerId = null;
  }

  async saveCustomer(): Promise<void> {
    this.clearNotices();
    this.savingCustomer = true;
    try {
      if (this.editingCustomerId === null) {
        await firstValueFrom(this.orderService.createCustomer(this.customerDraft));
        this.successMessage = 'Customer added.';
      } else {
        await firstValueFrom(this.orderService.updateCustomer(this.editingCustomerId, this.customerDraft));
        this.successMessage = 'Customer updated.';
      }
      this.cancelCustomerEdit();
      await this.loadData();
    } catch (error) {
      this.errorMessage = this.errorText(error, 'Could not save the customer.');
    } finally {
      this.savingCustomer = false;
    }
  }

  async deleteCustomer(customer: Customer): Promise<void> {
    this.clearNotices();
    this.pendingDeletion = { kind: 'customer', customer };
  }

  cancelDeletion(): void {
    if (this.deletingRecord) {
      return;
    }
    this.pendingDeletion = null;
  }

  async confirmDeletion(): Promise<void> {
    const deletion = this.pendingDeletion;
    if (!deletion || this.deletingRecord ||
      (deletion.kind === 'customer' && this.ordersBlockingCustomerDelete.length > 0)) {
      return;
    }
    this.clearNotices();
    this.deletingRecord = true;
    try {
      if (deletion.kind === 'order') {
        await firstValueFrom(this.orderService.deleteOrder(deletion.order.id));
        this.successMessage = `Order #${deletion.order.id} deleted.`;
      } else {
        await firstValueFrom(this.orderService.deleteCustomer(deletion.customer.id));
        this.successMessage = `${deletion.customer.name} deleted.`;
      }
      this.pendingDeletion = null;
      await this.loadData();
    } catch (error) {
      this.errorMessage = this.errorText(error, 'Could not delete the selected record.');
    } finally {
      this.deletingRecord = false;
    }
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0].toUpperCase()).join('');
  }

  avatarClass(name: string): string {
    return `tone-${name.charCodeAt(0) % 4}`;
  }

  customerEmail(customerId: number | null): string {
    if (customerId === null) {
      return 'Customer record deleted';
    }
    const customer = this.customers.find((entry) => entry.id === customerId);
    return customer?.email || customer?.phone || 'No contact details';
  }

  itemSummary(order: Order): string {
    return order.items.map((item) => item.sku ? `${item.name} · ${item.sku}` : item.name).join(', ');
  }

  statusClass(status: OrderStatus): string {
    return `status-select status-${status.toLowerCase()}`;
  }

  availableStatusOptions(status: OrderStatus): OrderStatus[] {
    return [status, ...this.statusTransitions[status]];
  }

  get editingStatusOptions(): OrderStatus[] {
    return this.editingOrderStatus ? this.availableStatusOptions(this.editingOrderStatus) : [];
  }

  private emptyOrderDraft(): OrderRequest {
    return { customerId: 0, status: 'Pending', items: [{ name: '', sku: '', quantity: 1, unitPrice: 0 }] };
  }

  private emptyCustomerDraft(): CustomerRequest {
    return { name: '', email: '', phone: '' };
  }

  private clearNotices(): void {
    this.errorMessage = '';
    this.orderSaveToast = '';
    this.successMessage = '';
  }

  private errorText(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const body = error.error;
      if (typeof body === 'object' && body !== null && 'message' in body && typeof body.message === 'string') {
        return body.message;
      }
    }
    return fallback;
  }
}
