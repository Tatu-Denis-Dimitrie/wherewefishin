import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import {
  Booking,
  BookedPeriod,
  CreateBookingRequest,
  ManagerTodaySummary,
  PaymentConfiguration,
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
  private managerTodaySummaryCache$: Observable<ManagerTodaySummary> | null = null;

  constructor(private http: HttpClient) {}

  getMyBookings(): Observable<Booking[]> {
    if (!this.myBookingsCache$) {
      this.myBookingsCache$ = this.http.get<Booking[]>(this.apiUrl).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }
    return this.myBookingsCache$;
  }

  getManagerTodaySummary(): Observable<ManagerTodaySummary> {
    if (!this.managerTodaySummaryCache$) {
      this.managerTodaySummaryCache$ = this.http.get<ManagerTodaySummary>(`${this.apiUrl}/manager/today-summary`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }

    return this.managerTodaySummaryCache$;
  }

  clearCache(): void {
    this.myBookingsCache$ = null;
    this.managerTodaySummaryCache$ = null;
  }

  createBooking(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(this.apiUrl, request).pipe(
      tap(() => this.clearCache())
    );
  }

  createPaymentIntent(request: CreatePaymentIntentRequest): Observable<PaymentIntentResponse> {
    return this.http.post<PaymentIntentResponse>(`${this.apiUrl}/payment-intent`, request);
  }

  getPaymentConfiguration(): Observable<PaymentConfiguration> {
    return this.http.get<PaymentConfiguration>(`${this.apiUrl}/payment-configuration`);
  }

  cancelBooking(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.clearCache())
    );
  }

  getBookedPeriods(pontoonId?: number, spotId?: number): Observable<BookedPeriod[]> {
    let params = new HttpParams();
    if (pontoonId) params = params.set('pontoonId', pontoonId.toString());
    else if (spotId) params = params.set('spotId', spotId.toString());
    return this.http.get<BookedPeriod[]>(`${this.apiUrl}/booked-periods`, { params });
  }
}
