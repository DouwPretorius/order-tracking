import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { of, Subject, throwError } from 'rxjs';
import { OrderService } from './order.service';
import { Customer, Order } from './order.models';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [{
        provide: OrderService,
        useValue: {
          getCustomers: () => of([{
            id: 1,
            name: 'John Doe',
            email: 'john.doe@example.com',
            phone: null,
            createdAt: '2026-10-03T00:00:00Z',
          }]),
          getOrders: () => of([{
            id: 17,
            customerId: 1,
            customerName: 'John Doe',
            status: 'Pending',
            createdAt: '2026-10-03T00:00:00Z',
            updatedAt: '2026-10-03T00:00:00Z',
            items: [{
              id: 1,
              name: 'Catan: Seafarers',
              sku: 'BG-1042',
              quantity: 2,
              unitPrice: 34.99,
              lineTotal: 69.98,
            }],
            total: 69.98,
          }]),
          createOrder: () => of({}),
          updateCustomer: () => throwError(() => ({
            status: 409,
            error: { message: 'A customer with this email address already exists.' },
          })),
        },
      }],
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render orders after the asynchronous API response', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Orders');
    expect(compiled.textContent).toContain('#17');
    expect(compiled.textContent).toContain('John Doe');
    expect(compiled.textContent).toContain('Catan: Seafarers');
  });

  it('should open row actions from the ellipsis without opening the order dialog', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    (compiled.querySelector('[aria-label="More actions for order 17"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(compiled.querySelector('.modal')).toBeNull();
    expect(compiled.querySelector('.row-actions.open')).not.toBeNull();

    (compiled.querySelector('.row-actions.open button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(compiled.querySelector('#order-dialog-title')?.textContent).toContain('Edit order');
    expect(compiled.querySelector('.row-actions.open')).toBeNull();
  });

  it('should open order details in a read-only dialog from the order number', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    (compiled.querySelector('.order-link') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(compiled.querySelector('#order-dialog-title')?.textContent).toContain('Order details');
    expect((compiled.querySelector('[aria-label="Customer"]') as HTMLInputElement).readOnly).toBe(true);
    expect((compiled.querySelector('[aria-label="Item name"]') as HTMLInputElement).readOnly).toBe(true);
    expect((compiled.querySelector('[aria-label="Quantity"]') as HTMLInputElement).readOnly).toBe(true);
    expect(compiled.querySelector('button[type="submit"]')).toBeNull();
    expect(compiled.querySelector('button.remove-item')).toBeNull();
    expect(compiled.textContent).toContain('Close');
  });

  it('should reject fractional item quantities before submitting', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const app = fixture.componentInstance;
    app.orderDraft = {
      customerId: 1,
      status: 'Pending',
      items: [{ name: 'Test item', sku: '', quantity: 1.5, unitPrice: 10 }],
    };

    await app.saveOrder();

    expect(app.errorMessage).toContain('positive whole number');
  });

  it('should clear order validation messages when the order dialog closes', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const app = fixture.componentInstance;
    app.openNewOrder();
    app.errorMessage = 'Each non-empty SKU can only appear once in an order.';
    fixture.detectChanges();

    expect(app.errorMessage).toContain('SKU');

    app.closeOrderDialog();
    fixture.detectChanges();

    expect(app.errorMessage).toBe('');
    expect(fixture.nativeElement.querySelector('.page-content [role="alert"]')).toBeNull();
  });

  it('should close the order dialog as soon as the save succeeds without waiting for refresh', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const app = fixture.componentInstance;
    const orderService = TestBed.inject(OrderService);
    const customersRefresh = new Subject<Customer[]>();
    const ordersRefresh = new Subject<Order[]>();
    orderService.getCustomers = () => customersRefresh.asObservable();
    orderService.getOrders = () => ordersRefresh.asObservable();

    app.openNewOrder();
    app.orderDraft = {
      customerId: 1,
      status: 'Pending',
      items: [{ name: 'Test item', sku: '', quantity: 1, unitPrice: 10 }],
    };
    fixture.detectChanges();

    await app.saveOrder();
    fixture.detectChanges();

    expect(app.orderDialogOpen).toBe(false);
    expect(app.savingOrder).toBe(false);
    expect(fixture.nativeElement.querySelector('.modal')).toBeNull();

    customersRefresh.next([]);
    customersRefresh.complete();
    ordersRefresh.next([]);
    ordersRefresh.complete();
    await fixture.whenStable();
  });

  it('should show a duplicate-order toast and allow editing and retrying the order', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const app = fixture.componentInstance;
    const orderService = TestBed.inject(OrderService);
    orderService.createOrder = () => throwError(() => ({
      status: 409,
      error: { message: 'An identical order was just submitted as order #17. No new order was created.' },
    }));

    const newOrderButton = [...fixture.nativeElement.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('New order')) as HTMLButtonElement;
    newOrderButton.click();
    app.orderDraft = {
      customerId: 1,
      status: 'Pending',
      items: [{ name: 'Test item', sku: '', quantity: 1, unitPrice: 10 }],
    };
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(app.orderSaveToast).toContain('An identical order was just submitted as order #17.');
    const toast = fixture.nativeElement.querySelector('.toast-overlay [role="alertdialog"]') as HTMLElement;
    expect(toast.textContent).toContain('An identical order was just submitted as order #17.');
    expect(toast.textContent).toContain('Please review or edit the order and try again.');
    expect(toast.closest('.modal')).toBeNull();
    expect(app.orderDialogOpen).toBe(true);
    expect(app.savingOrder).toBe(false);
    expect(fixture.nativeElement.querySelector('.modal')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.toast-overlay')).not.toBeNull();

    (toast.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(app.orderSaveToast).toBe('');
    expect(fixture.nativeElement.querySelector('.toast-overlay')).toBeNull();

    app.orderDraft.items[0].name = 'Edited test item';
    orderService.createOrder = () => of({
      id: 18,
      customerId: 1,
      customerName: 'John Doe',
      status: 'Pending',
      createdAt: '2026-10-04T00:00:00Z',
      updatedAt: '2026-10-04T00:00:00Z',
      items: [],
      total: 10,
    });
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(app.orderDialogOpen).toBe(false);
    expect(app.orderSaveToast).toBe('');
  });

  it('should offer only valid next statuses and keep terminal statuses locked', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;

    expect(app.availableStatusOptions('Pending')).toEqual(['Pending', 'Processing', 'Cancelled']);
    expect(app.availableStatusOptions('Processing')).toEqual(['Processing', 'Confirmed', 'Cancelled']);
    expect(app.availableStatusOptions('Confirmed')).toEqual(['Confirmed', 'Completed', 'Cancelled']);
    expect(app.availableStatusOptions('Completed')).toEqual(['Completed']);
    expect(app.availableStatusOptions('Cancelled')).toEqual(['Cancelled']);
  });

  it('should show blocking orders in the delete toast and prevent customer deletion', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const app = fixture.componentInstance;
    await app.loadData();
    const customer = app.customers[0];
    expect(customer).toBeTruthy();
    app.orders.push({
      id: 18,
      customerId: customer.id,
      customerName: customer.name,
      status: 'Completed',
      createdAt: '2026-10-03T00:00:00Z',
      updatedAt: '2026-10-03T00:00:00Z',
      items: [],
      total: 0,
    });
    await app.deleteCustomer(customer);
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('.delete-confirm-toast[role="alertdialog"]') as HTMLElement;
    expect(app.ordersBlockingCustomerDelete.map((order) => order.id)).toEqual([17]);
    expect(dialog.textContent).toContain('Order #17');
    expect(dialog.textContent).not.toContain('Order #18');
    expect(dialog.querySelector('.toast-delete')).toBeNull();
    expect(dialog.querySelector('.toast-cancel')).not.toBeNull();
  });

  it('should confirm order deletion from the toast and dismiss it on cancel', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const app = fixture.componentInstance;
    const orderService = TestBed.inject(OrderService);
    const deleteOrder = vi.fn(() => of(void 0));
    orderService.deleteOrder = deleteOrder;
    const order = app.orders[0];
    const actionsButton = fixture.nativeElement.querySelector(
      `[aria-label="More actions for order ${order.id}"]`,
    ) as HTMLButtonElement;
    actionsButton.click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.row-actions.open .delete-action') as HTMLButtonElement).click();

    fixture.detectChanges();
    const confirmation = fixture.nativeElement.querySelector('.delete-confirm-toast') as HTMLElement;
    expect(confirmation.textContent).toContain(`Delete order #${order.id}?`);
    expect(deleteOrder).not.toHaveBeenCalled();

    (confirmation.querySelector('.toast-cancel') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(app.pendingDeletion).toBeNull();
    expect(deleteOrder).not.toHaveBeenCalled();

    actionsButton.click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.row-actions.open .delete-action') as HTMLButtonElement).click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.toast-delete') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(deleteOrder).toHaveBeenCalledWith(order.id);
    expect(app.pendingDeletion).toBeNull();
  });

  it('should confirm customer deletion from the toast when no incomplete orders block it', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const app = fixture.componentInstance;
    await app.loadData();
    app.orders[0].status = 'Completed';
    const orderService = TestBed.inject(OrderService);
    const deleteCustomer = vi.fn(() => of(void 0));
    orderService.deleteCustomer = deleteCustomer;

    app.openCustomers();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.customer-row .delete-action') as HTMLButtonElement).click();
    fixture.detectChanges();

    const confirmation = fixture.nativeElement.querySelector('.delete-confirm-toast') as HTMLElement;
    expect(confirmation.textContent).toContain('Delete John Doe?');
    (confirmation.querySelector('.toast-delete') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(deleteCustomer).toHaveBeenCalledWith(1);
    expect(app.pendingDeletion).toBeNull();
  });

  it('should show duplicate-email feedback and keep the customer edit form open', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const app = fixture.componentInstance;
    await app.loadData();
    app.openCustomers();
    app.editCustomer(app.customers[0]);
    app.customerDraft.email = 'already-used@example.com';
    fixture.detectChanges();

    await app.saveCustomer();
    fixture.detectChanges();

    expect(app.customerDialogOpen).toBe(true);
    const dialog = fixture.nativeElement.querySelector('.customer-modal') as HTMLElement;
    expect(dialog.querySelector('[role="alert"]')?.textContent)
      .toContain('A customer with this email address already exists.');
    expect(dialog.querySelector('form.customer-form')).not.toBeNull();
    expect(app.editingCustomerId).toBe(app.customers[0].id);
  });
});
