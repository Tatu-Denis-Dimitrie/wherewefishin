import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
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

  constructor(private http: HttpClient) {}

  getMyBookings(): Observable<Booking[]> {
    return this.http.get<Booking[]>(this.apiUrl);
  }

  createBooking(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(this.apiUrl, request);
  }

  createPaymentIntent(request: CreatePaymentIntentRequest): Observable<PaymentIntentResponse> {
    return this.http.post<PaymentIntentResponse>(`${this.apiUrl}/payment-intent`, request);
  }

  cancelBooking(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
