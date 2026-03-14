import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  loadStripe,
  Stripe,
  StripeCardElement,
  StripeCardElementChangeEvent,
  StripeElements
} from '@stripe/stripe-js';
import { CartService } from '../../services/cart.service';
import { BookingService } from '../../services/booking.service';
import { AuthService } from '../../services/auth.service';
import { CartItem, Booking } from '../../models/booking.model';
import { environment } from '../../../environments/environment';
import QRCode from 'qrcode';

@Component({
  selector: 'app-cart',
  imports: [CommonModule, FormsModule],
  templateUrl: './cart.html',
  styleUrl: './cart.css'
})
export class Cart implements OnInit, OnDestroy {
  @ViewChild('cardElementHost')
  set cardElementHost(element: ElementRef<HTMLDivElement> | undefined) {
    this.cardMountElement = element;

    if (!element) {
      this.cardElement?.unmount();
      this.stripeReady = false;
      return;
    }

    void this.mountCardElement();
  }

  readonly DURATIONS = [12, 24, 48, 72];

  showMessage = '';
  messageType: 'success' | 'error' = 'success';

  activeTab: 'cart' | 'bookings' = 'cart';

  myBookings: Booking[] = [];
  loadingBookings = false;

  checkingOut = false;

  stripeReady = false;
  stripeError = '';
  cardValidationError = '';

  qrCodeMap: Record<number, string> = {};
  expandedQr = new Set<number>();

  private stripe: Stripe | null = null;
  private stripeElements: StripeElements | null = null;
  private cardElement: StripeCardElement | null = null;
  private cardMountElement?: ElementRef<HTMLDivElement>;

  constructor(
    public cartService: CartService,
    private bookingService: BookingService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadBookings();
  }

  ngOnDestroy(): void {
    this.cardElement?.destroy();
  }

  private async mountCardElement(): Promise<void> {
    if (!this.cardMountElement) {
      this.stripeReady = false;
      return;
    }

    if (!environment.stripePublishableKey) {
      this.stripeReady = false;
      this.stripeError = 'Cheia Stripe publishable nu este configurată.';
      return;
    }

    this.stripeError = '';

    try {
      if (!this.stripe) {
        this.stripe = await loadStripe(environment.stripePublishableKey);
      }

      if (!this.stripe) {
        this.stripeReady = false;
        this.stripeError = 'Nu s-a putut inițializa Stripe.';
        return;
      }

      if (!this.stripeElements) {
        this.stripeElements = this.stripe.elements();
      }

      if (!this.cardElement) {
        this.cardElement = this.stripeElements.create('card', {
          hidePostalCode: true
        });

        this.cardElement.on('change', (event: StripeCardElementChangeEvent) => {
          this.cardValidationError = event.error?.message ?? '';
        });
      }

      this.cardElement.unmount();
      this.cardElement.mount(this.cardMountElement.nativeElement);
      this.stripeReady = true;
    } catch {
      this.stripeReady = false;
      this.stripeError = 'Nu s-a putut încărca formularul de plată Stripe.';
    }
  }

  loadBookings(): void {
    this.loadingBookings = true;
    this.bookingService.getMyBookings().subscribe({
      next: (bookings) => {
        this.myBookings = bookings;
        this.loadingBookings = false;
        this.generateQrCodes(bookings);
      },
      error: () => {
        this.loadingBookings = false;
      }
    });
  }

  private async generateQrCodes(bookings: Booking[]): Promise<void> {
    for (const booking of bookings) {
      if (this.qrCodeMap[booking.id]) continue;
      const content = [
        `WhereWeFishin - Booking #${booking.id}`,
        `Username: ${this.authService.getUsername()}`,
        `Booking ID: #${booking.id}`,
        `Spot: ${booking.fishingSpotName}`,
        `Start: ${new Date(booking.startDate).toLocaleString('ro-RO')}`,
        `Duration: ${booking.durationHours}h`,
        `Total: ${booking.totalPrice.toFixed(2)} RON`,
        `Status: ${booking.status}`
      ].join('\n');
      this.qrCodeMap[booking.id] = await QRCode.toDataURL(content, { width: 180, margin: 1 });
    }
  }

  toggleQr(id: number): void {
    if (this.expandedQr.has(id)) {
      this.expandedQr.delete(id);
    } else {
      this.expandedQr.add(id);
    }
  }

  isQrExpanded(id: number): boolean {
    return this.expandedQr.has(id);
  }

  updateDuration(spotId: number, durationStr: string, pontoonId?: number): void {
    const duration = parseInt(durationStr, 10);
    this.cartService.updateItem(spotId, { durationHours: duration }, pontoonId);
  }

  updateStartDate(spotId: number, dateStr: string, pontoonId?: number): void {
    this.cartService.updateItem(spotId, { startDate: dateStr }, pontoonId);
  }

  removeFromCart(spotId: number, pontoonId?: number): void {
    this.cartService.removeItem(spotId, pontoonId);
  }

  activateCartTab(): void {
    this.activeTab = 'cart';
    setTimeout(() => {
      void this.mountCardElement();
    });
  }

  itemTotal(item: CartItem): number {
    return item.pricePerHour * item.durationHours;
  }

  async checkout(): Promise<void> {
    const items = [...this.cartService.items()];
    if (items.length === 0) return;

    // Validate start dates
    const now = new Date();
    for (const item of items) {
      if (!item.startDate) {
        this.showToast('Setează data de start pentru toate rezervările', 'error');
        return;
      }

      if (this.itemTotal(item) <= 0) {
        const itemName = item.pontoonName ? `${item.spotName} - ${item.pontoonName}` : item.spotName;
        this.showToast(`Rezervarea "${itemName}" are preț invalid (0 RON). Actualizează prețul pe locație înainte de checkout.`, 'error');
        return;
      }

      if (new Date(item.startDate) < new Date(now.getTime() - 5 * 60 * 1000)) {
        const itemName = item.pontoonName ? `${item.spotName} - ${item.pontoonName}` : item.spotName;
        this.showToast(`Data de start pentru "${itemName}" nu poate fi în trecut`, 'error');
        return;
      }
    }

    if (this.cardValidationError) {
      this.showToast('Datele cardului nu sunt valide.', 'error');
      return;
    }

    if (!this.stripeReady || !this.stripe || !this.cardElement) {
      await this.mountCardElement();
    }

    if (!this.stripeReady || !this.stripe || !this.cardElement) {
      this.showToast(this.stripeError || 'Formularul de plată nu este disponibil.', 'error');
      return;
    }

    this.checkingOut = true;
    let completed = 0;
    let errors = 0;
    let firstError = '';

    for (const item of items) {
      try {
        const paymentIntentResponse = await firstValueFrom(this.bookingService.createPaymentIntent({
          fishingSpotId: item.spotId,
          pontoonId: item.pontoonId,
          startDate: new Date(item.startDate).toISOString(),
          durationHours: item.durationHours
        }));

        const paymentResult = await this.stripe.confirmCardPayment(paymentIntentResponse.clientSecret, {
          payment_method: {
            card: this.cardElement,
            billing_details: {
              name: this.authService.getUsername() || undefined
            }
          }
        });

        if (paymentResult.error) {
          throw new Error(paymentResult.error.message ?? 'Plata a eșuat.');
        }

        const paymentIntent = paymentResult.paymentIntent;
        if (!paymentIntent?.id || paymentIntent.status !== 'succeeded') {
          throw new Error('Plata nu a fost confirmată de Stripe.');
        }

        await firstValueFrom(this.bookingService.createBooking({
          fishingSpotId: item.spotId,
          pontoonId: item.pontoonId,
          startDate: new Date(item.startDate).toISOString(),
          durationHours: item.durationHours,
          paymentIntentId: paymentIntent.id
        }));

        completed++;
        this.cartService.removeItem(item.spotId, item.pontoonId);
      } catch (error: unknown) {
        errors++;
        if (!firstError) {
          firstError = this.getErrorMessage(error);
        }
      }
    }

    this.checkingOut = false;

    if (errors === 0) {
      this.showToast('Toate rezervările au fost confirmate și plătite!', 'success');
      this.activeTab = 'bookings';
      this.loadBookings();
      return;
    }

    if (completed === 0) {
      this.showToast(firstError || 'Checkout-ul a eșuat.', 'error');
      return;
    }

    this.showToast(
      `${completed} rezervare(i) confirmate, ${errors} eșuate${firstError ? `: ${firstError}` : ''}`,
      'error'
    );
    this.activeTab = 'bookings';
    this.loadBookings();
  }

  cancelBooking(id: number): void {
    if (!confirm('Are you sure you want to cancel this booking?')) return;
    this.bookingService.cancelBooking(id).subscribe({
      next: () => {
        // Invalidate cached QR so it's regenerated with "Cancelled" status
        delete this.qrCodeMap[id];
        this.showToast('Booking cancelled', 'success');
        this.loadBookings();
      },
      error: () => this.showToast('Failed to cancel booking', 'error')
    });
  }

  getMinDate(): string {
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    return now.toISOString().slice(0, 16);
  }

  statusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'confirmed': return 'status-confirmed';
      case 'cancelled': return 'status-cancelled';
      default: return 'status-pending';
    }
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const payload = error.error ?? error.message ?? 'Eroare necunoscută';
      return typeof payload === 'string' ? payload : JSON.stringify(payload);
    }

    if (error instanceof Error) {
      return error.message;
    }

    return 'Eroare necunoscută';
  }

  private showToast(message: string, type: 'success' | 'error'): void {
    this.showMessage = message;
    this.messageType = type;
    setTimeout(() => this.showMessage = '', 3500);
  }

  goToMap(): void {
    this.router.navigate(['/home']);
  }
}
