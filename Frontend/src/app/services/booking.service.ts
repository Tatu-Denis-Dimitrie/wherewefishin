import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import {
  Booking,
  CreateBookingRequest,
  CreatePaymentIntentRequest,
  PaymentIntentResponse
} from '../models/booking.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class BookingService {
  private apiUrl = `${environment.apiBaseUrl}/api/bookings`;
  private myBookingsCache$: Observable<Booking[]> | null = null;

  constructor(private http: HttpClient) {}

  getMyBookings(): Observable<Booking[]> {
    if (!this.myBookingsCache$) {
      this.myBookingsCache$ = this.http.get<Booking[]>(this.apiUrl).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }
    return this.myBookingsCache$;
  }

  clearCache(): void {
    this.myBookingsCache$ = null;
  }

  createBooking(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(this.apiUrl, request).pipe(
      tap(() => this.clearCache())
    );
  }

  createPaymentIntent(request: CreatePaymentIntentRequest): Observable<PaymentIntentResponse> {
    return this.http.post<PaymentIntentResponse>(`${this.apiUrl}/payment-intent`, request);
  }

  cancelBooking(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.clearCache())
    );
  }
}
