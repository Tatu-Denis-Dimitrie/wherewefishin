import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { BookingService } from '../../services/booking.service';
import { AuthService } from '../../services/auth.service';
import { Booking } from '../../models/booking.model';
import { buildBookingQrPayload } from '../../shared/qr/booking-qr';
import QRCode from 'qrcode';

type PaginationItem = number | 'ellipsis-left' | 'ellipsis-right';

@Component({
  selector: 'app-my-bookings',
  imports: [CommonModule],
  templateUrl: './my-bookings.html',
  styleUrl: './my-bookings.css'
})
export class MyBookings implements OnInit {
  myBookings: Booking[] = [];
  pagedBookings: Booking[] = [];
  loadingBookings = false;
  currentPage = 1;
  readonly pageSize = 10;
  totalItems = 0;
  totalPages = 0;
  hasPreviousPage = false;
  hasNextPage = false;

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
    if (!this.authService.isUser()) {
      this.router.navigate(['/home']);
      return;
    }

    this.loadBookings();
  }

  loadBookings(): void {
    this.loadingBookings = true;
    this.bookingService.clearCache();
    this.bookingService.getMyBookings().subscribe({
      next: (bookings) => {
        this.myBookings = bookings;
        this.applyPagination();
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
      const content = buildBookingQrPayload(booking, this.authService.getUsername());
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

  get pageNumbers(): number[] {
    return Array.from({ length: this.totalPages }, (_, index) => index + 1);
  }

  get visiblePageItems(): PaginationItem[] {
    if (this.totalPages <= 5) {
      return this.pageNumbers;
    }

    const items: PaginationItem[] = [1];
    const startPage = Math.max(2, this.currentPage - 1);
    const endPage = Math.min(this.totalPages - 1, this.currentPage + 1);

    if (startPage > 2) {
      items.push('ellipsis-left');
    }

    for (let page = startPage; page <= endPage; page++) {
      items.push(page);
    }

    if (endPage < this.totalPages - 1) {
      items.push('ellipsis-right');
    }

    items.push(this.totalPages);
    return items;
  }

  get pageStartItem(): number {
    if (!this.totalItems) {
      return 0;
    }

    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get pageEndItem(): number {
    return this.pageStartItem + this.pagedBookings.length - 1;
  }

  isPageNumber(item: PaginationItem): item is number {
    return typeof item === 'number';
  }

  changePage(page: number): void {
    const nextPage = Math.min(Math.max(page, 1), Math.max(this.totalPages, 1));
    if (nextPage === this.currentPage) {
      return;
    }

    this.applyPagination(nextPage);
    window.scrollTo({ top: 0, behavior: 'smooth' });
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

  private applyPagination(page = this.currentPage): void {
    this.totalItems = this.myBookings.length;
    this.totalPages = Math.ceil(this.totalItems / this.pageSize);
    this.currentPage = this.totalItems
      ? Math.min(Math.max(page, 1), this.totalPages)
      : 1;

    const startIndex = (this.currentPage - 1) * this.pageSize;
    this.pagedBookings = this.myBookings.slice(startIndex, startIndex + this.pageSize);
    this.hasPreviousPage = this.currentPage > 1;
    this.hasNextPage = this.currentPage < this.totalPages;
  }

  private showToast(message: string, type: 'success' | 'error'): void {
    this.showMessage = message;
    this.messageType = type;
    setTimeout(() => this.showMessage = '', 3500);
  }
}
