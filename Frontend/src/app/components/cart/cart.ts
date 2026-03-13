import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { CartService } from '../../services/cart.service';
import { BookingService } from '../../services/booking.service';
import { AuthService } from '../../services/auth.service';
import { CartItem, Booking } from '../../models/booking.model';
import QRCode from 'qrcode';

@Component({
  selector: 'app-cart',
  imports: [CommonModule, FormsModule],
  templateUrl: './cart.html',
  styleUrl: './cart.css'
})
export class Cart implements OnInit {
  readonly DURATIONS = [12, 24, 48, 72];

  showMessage = '';
  messageType: 'success' | 'error' = 'success';

  activeTab: 'cart' | 'bookings' = 'cart';

  myBookings: Booking[] = [];
  loadingBookings = false;

  checkingOut = false;

  qrCodeMap: Record<number, string> = {};
  expandedQr = new Set<number>();

  constructor(
    public cartService: CartService,
    private bookingService: BookingService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadBookings();
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

  itemTotal(item: CartItem): number {
    return item.pricePerHour * item.durationHours;
  }

  checkout(): void {
    const items = this.cartService.items();
    if (items.length === 0) return;

    // Validate start dates
    const now = new Date();
    for (const item of items) {
      if (!item.startDate) {
        this.showToast('Setează data de start pentru toate rezervările', 'error');
        return;
      }
      if (new Date(item.startDate) < new Date(now.getTime() - 5 * 60 * 1000)) {
        const itemName = item.pontoonName ? `${item.spotName} - ${item.pontoonName}` : item.spotName;
        this.showToast(`Data de start pentru "${itemName}" nu poate fi în trecut`, 'error');
        return;
      }
    }

    this.checkingOut = true;
    let completed = 0;
    let errors = 0;

    for (const item of items) {
      this.bookingService.createBooking({
        fishingSpotId: item.spotId,
        pontoonId: item.pontoonId,
        startDate: new Date(item.startDate).toISOString(),
        durationHours: item.durationHours
      }).subscribe({
        next: () => {
          completed++;
          this.cartService.removeItem(item.spotId, item.pontoonId);
          if (completed + errors === items.length) {
            this.checkingOut = false;
            if (errors === 0) {
              this.showToast('Toate rezervările au fost confirmate!', 'success');
              this.activeTab = 'bookings';
              this.loadBookings();
            } else {
              this.showToast(`${completed} rezervare(i) confirmate, ${errors} eșuate`, 'error');
              this.loadBookings();
            }
          }
        },
        error: (err: HttpErrorResponse) => {
          errors++;
          const msg = err.error ?? err.message ?? 'Eroare necunoscută';
          const errorText = typeof msg === 'string' ? msg : JSON.stringify(msg);
          if (completed + errors === items.length) {
            this.checkingOut = false;
            this.showToast(
              errors === items.length
                ? errorText
                : `${completed} rezervare(i) confirmate, ${errors} eșuate: ${errorText}`,
              'error'
            );
            this.loadBookings();
          }
        }
      });
    }
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

  private showToast(message: string, type: 'success' | 'error'): void {
    this.showMessage = message;
    this.messageType = type;
    setTimeout(() => this.showMessage = '', 3500);
  }

  goToMap(): void {
    this.router.navigate(['/home']);
  }
}
