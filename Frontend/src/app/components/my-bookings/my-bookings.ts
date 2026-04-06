import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { BookingService } from '../../services/booking.service';
import { AuthService } from '../../services/auth.service';
import { Booking } from '../../models/booking.model';
import QRCode from 'qrcode';

@Component({
  selector: 'app-my-bookings',
  imports: [CommonModule],
  templateUrl: './my-bookings.html',
  styleUrl: './my-bookings.css'
})
export class MyBookings implements OnInit {
  myBookings: Booking[] = [];
  loadingBookings = false;

  showMessage = '';
  messageType: 'success' | 'error' = 'success';

  qrCodeMap: Record<number, string> = {};
  expandedQr = new Set<number>();
  enlargedQrId: number | null = null;

  constructor(
    private bookingService: BookingService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadBookings();
  }

  loadBookings(): void {
    this.loadingBookings = true;
    this.bookingService.clearCache();
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
      const content = JSON.stringify({
        bookingId: booking.id,
        token: booking.verificationToken || '',
        spot: booking.fishingSpotName,
        user: this.authService.getUsername()
      });
      this.qrCodeMap[booking.id] = await QRCode.toDataURL(content, { width: 420, margin: 2 });
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

  openQrModal(id: number): void {
    if (!this.qrCodeMap[id]) return;
    this.enlargedQrId = id;
  }

  closeQrModal(): void {
    this.enlargedQrId = null;
  }

  cancelBooking(id: number): void {
    if (!confirm('Are you sure you want to cancel this booking?')) return;
    this.bookingService.cancelBooking(id).subscribe({
      next: () => {
        delete this.qrCodeMap[id];
        if (this.enlargedQrId === id) {
          this.enlargedQrId = null;
        }
        this.showToast('Booking cancelled', 'success');
        this.loadBookings();
      },
      error: () => this.showToast('Failed to cancel booking', 'error')
    });
  }

  statusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'confirmed': return 'status-confirmed';
      case 'cancelled': return 'status-cancelled';
      default: return 'status-pending';
    }
  }

  goToMap(): void {
    this.router.navigate(['/home']);
  }

  private showToast(message: string, type: 'success' | 'error'): void {
    this.showMessage = message;
    this.messageType = type;
    setTimeout(() => this.showMessage = '', 3500);
  }
}
