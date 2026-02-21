import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CartService } from '../../services/cart.service';
import { BookingService } from '../../services/booking.service';
import { AuthService } from '../../services/auth.service';
import { CartItem, Booking } from '../../models/booking.model';

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
      },
      error: () => {
        this.loadingBookings = false;
      }
    });
  }

  updateDuration(spotId: number, durationStr: string): void {
    const duration = parseInt(durationStr, 10);
    this.cartService.updateItem(spotId, { durationHours: duration });
  }

  updateStartDate(spotId: number, dateStr: string): void {
    this.cartService.updateItem(spotId, { startDate: dateStr });
  }

  removeFromCart(spotId: number): void {
    this.cartService.removeItem(spotId);
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
        this.showToast('Please set a start date for all spots', 'error');
        return;
      }
      if (new Date(item.startDate) < new Date(now.getTime() - 5 * 60 * 1000)) {
        this.showToast(`Start date for "${item.spotName}" cannot be in the past`, 'error');
        return;
      }
    }

    this.checkingOut = true;
    let completed = 0;
    let errors = 0;

    for (const item of items) {
      this.bookingService.createBooking({
        fishingSpotId: item.spotId,
        startDate: new Date(item.startDate).toISOString(),
        durationHours: item.durationHours
      }).subscribe({
        next: () => {
          completed++;
          this.cartService.removeItem(item.spotId);
          if (completed + errors === items.length) {
            this.checkingOut = false;
            if (errors === 0) {
              this.showToast('All bookings confirmed!', 'success');
              this.activeTab = 'bookings';
              this.loadBookings();
            } else {
              this.showToast(`${completed} booking(s) confirmed, ${errors} failed`, 'error');
              this.loadBookings();
            }
          }
        },
        error: () => {
          errors++;
          if (completed + errors === items.length) {
            this.checkingOut = false;
            this.showToast(`${completed} booking(s) confirmed, ${errors} failed`, 'error');
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
