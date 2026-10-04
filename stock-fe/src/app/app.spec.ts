import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { of, throwError } from 'rxjs';
import { OrderService } from './order.service';

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
    const app = fixture.componentInstance;
    app.orderDraft = {
      customerId: 1,
      status: 'Pending',
      items: [{ name: 'Test item', sku: '', quantity: 1.5, unitPrice: 10 }],
    };

    await app.saveOrder();

    expect(app.errorMessage).toContain('positive whole number');
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

  it('should show blocking orders and disable customer deletion', async () => {
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
    app.customerDialogOpen = true;

    await app.deleteCustomer(customer);
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="alertdialog"]') as HTMLElement;
    const deleteButton = [...dialog.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Delete customer'));
    expect(app.ordersBlockingCustomerDelete.map((order) => order.id)).toEqual([17]);
    expect(dialog.textContent).toContain('Order #17');
    expect(dialog.textContent).not.toContain('Order #18');
    expect(deleteButton?.disabled).toBe(true);
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
