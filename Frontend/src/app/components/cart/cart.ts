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
import { BookedPeriod, CartItem } from '../../models/booking.model';
import { environment } from '../../../environments/environment';

type CalendarDay = {
  date: Date;
  dayNum: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  isBooked: boolean;
  isPartiallyBooked: boolean;
  bookedHours: number;
  bookedIntervals: string;
  isPast: boolean;
  isSelected: boolean;
  isInRange: boolean;
  isRangeStart: boolean;
  isRangeEnd: boolean;
};

interface ItemCalendar {
  calendarMonth: Date;
  calendarDays: CalendarDay[];
  bookedPeriods: BookedPeriod[];
  rangeStartDate: Date | null;
  rangeEndDate: Date | null;
  selectedHour: number;
  isCustomDuration: boolean;
}

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

    if (this.mountCardTimer) {
      clearTimeout(this.mountCardTimer);
      this.mountCardTimer = undefined;
    }

    if (!element) {
      this.cardElement?.unmount();
      this.stripeReady = false;
      return;
    }

    this.scheduleCardMount();
  }

  readonly DURATIONS = [12, 24, 48, 72];

  calendarStates = new Map<string, ItemCalendar>();
  calendarErrors = new Map<string, string>();

  showMessage = '';
  messageType: 'success' | 'error' = 'success';

  checkingOut = false;

  stripeReady = false;
  stripeError = '';
  cardValidationError = '';

  private stripe: Stripe | null = null;
  private stripeElements: StripeElements | null = null;
  private cardElement: StripeCardElement | null = null;
  private cardMountElement?: ElementRef<HTMLDivElement>;
  private mountCardTimer?: ReturnType<typeof setTimeout>;

  constructor(
    public cartService: CartService,
    private bookingService: BookingService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.scheduleCardMount();
    this.initCalendars();
  }

  ngOnDestroy(): void {
    if (this.mountCardTimer) {
      clearTimeout(this.mountCardTimer);
      this.mountCardTimer = undefined;
    }

    this.cardElement?.destroy();
  }

  private scheduleCardMount(): void {
    this.mountCardTimer = setTimeout(() => {
      this.mountCardTimer = undefined;
      void this.mountCardElement();
    });
  }

  private async mountCardElement(): Promise<void> {
    if (!this.cardMountElement) {
      this.stripeReady = false;
      return;
    }

    if (!environment.stripePublishableKey) {
      this.stripeReady = false;
      this.stripeError = 'Stripe publishable key is not configured.';
      return;
    }

    this.stripeError = '';

    try {
      if (!this.stripe) {
        this.stripe = await loadStripe(environment.stripePublishableKey);
      }

      if (!this.stripe) {
        this.stripeReady = false;
        this.stripeError = 'Could not initialize Stripe.';
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
      this.stripeError = 'Could not load Stripe payment form.';
    }
  }

  removeFromCart(spotId: number, pontoonId?: number): void {
    this.cartService.removeItem(spotId, pontoonId);
  }

  // ---- Per-item calendar state ----

  itemKey(item: CartItem): string {
    return `${item.spotId}-${item.pontoonId ?? 0}`;
  }

  getCalendar(item: CartItem): ItemCalendar {
    const key = this.itemKey(item);
    if (!this.calendarStates.has(key)) {
      this.initCalendarForItem(item);
    }
    return this.calendarStates.get(key)!;
  }

  private initCalendars(): void {
    for (const item of this.cartService.items()) {
      const key = this.itemKey(item);
      if (!this.calendarStates.has(key)) {
        this.initCalendarForItem(item);
      }
    }
  }

  private initCalendarForItem(item: CartItem): void {
    const existing = item.startDate ? new Date(item.startDate) : new Date();
    const rangeStart = this.normalizeToMidnight(existing);

    const state: ItemCalendar = {
      calendarMonth: new Date(existing.getFullYear(), existing.getMonth(), 1),
      calendarDays: [],
      bookedPeriods: [],
      rangeStartDate: rangeStart,
      rangeEndDate: null,
      selectedHour: existing.getHours() || 8,
      isCustomDuration: !this.DURATIONS.includes(item.durationHours)
    };

    if (state.isCustomDuration && item.durationHours > 24) {
      const endDate = new Date(existing);
      endDate.setHours(endDate.getHours() + item.durationHours);
      state.rangeEndDate = new Date(endDate.getFullYear(), endDate.getMonth(), endDate.getDate());
    }

    this.calendarStates.set(this.itemKey(item), state);
    this.buildCalendar(this.itemKey(item));
    this.loadBookedPeriodsForItem(item, state);
  }

  private loadBookedPeriodsForItem(item: CartItem, state: ItemCalendar): void {
    const obs = item.pontoonId
      ? this.bookingService.getBookedPeriods(item.pontoonId)
      : this.bookingService.getBookedPeriods(undefined, item.spotId);

    obs.subscribe({
      next: (periods) => {
        state.bookedPeriods = periods;
        this.buildCalendar(this.itemKey(item));
      },
      error: () => {
        state.bookedPeriods = [];
        this.buildCalendar(this.itemKey(item));
      }
    });
  }

  buildCalendar(key: string): void {
    const state = this.calendarStates.get(key);
    if (!state) return;

    const year = state.calendarMonth.getFullYear();
    const month = state.calendarMonth.getMonth();
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const today = this.normalizeToMidnight(new Date());

    const rangeStart = state.rangeStartDate ? this.normalizeToMidnight(state.rangeStartDate) : null;
    const rangeEnd = state.rangeEndDate ? this.normalizeToMidnight(state.rangeEndDate) : null;

    const days: CalendarDay[] = [];
    const startWeekday = (firstDay.getDay() + 6) % 7;
    for (let i = startWeekday - 1; i >= 0; i--) {
      const d = new Date(year, month, -i);
      days.push(this.makeCalendarDay(d, false, today, rangeStart, rangeEnd, state.bookedPeriods));
    }
    for (let d = 1; d <= lastDay.getDate(); d++) {
      const date = new Date(year, month, d);
      days.push(this.makeCalendarDay(date, true, today, rangeStart, rangeEnd, state.bookedPeriods));
    }
    const totalCells = Math.ceil(days.length / 7) * 7;
    let nextDay = 1;
    while (days.length < totalCells) {
      const d = new Date(year, month + 1, nextDay++);
      days.push(this.makeCalendarDay(d, false, today, rangeStart, rangeEnd, state.bookedPeriods));
    }
    state.calendarDays = days;
  }

  private makeCalendarDay(
    date: Date,
    isCurrentMonth: boolean,
    today: Date,
    rangeStart: Date | null,
    rangeEnd: Date | null,
    bookedPeriods: BookedPeriod[]
  ): CalendarDay {
    const dateMidnight = this.normalizeToMidnight(date);
    const dateEnd = new Date(dateMidnight);
    dateEnd.setHours(23, 59, 59, 999);
    const bookedHours = this.getBookedHoursForDay(bookedPeriods, dateMidnight, dateEnd);
    const isFullyBooked = bookedHours >= 24;
    const isPartiallyBooked = bookedHours > 0 && bookedHours < 24;
    const bookedIntervals = (isPartiallyBooked || isFullyBooked) ? this.getBookedIntervalsForDay(bookedPeriods, dateMidnight, dateEnd) : '';
    const isRangeStart = rangeStart ? dateMidnight.getTime() === rangeStart.getTime() : false;
    const isRangeEnd = rangeEnd ? dateMidnight.getTime() === rangeEnd.getTime() : false;
    const isInRange = rangeStart && rangeEnd
      ? dateMidnight >= rangeStart && dateMidnight <= rangeEnd
      : isRangeStart;
    return {
      date, dayNum: date.getDate(), isCurrentMonth,
      isToday: dateMidnight.getTime() === today.getTime(),
      isBooked: isFullyBooked, isPartiallyBooked, bookedHours, bookedIntervals,
      isPast: dateMidnight < today,
      isSelected: isRangeStart || isRangeEnd,
      isInRange, isRangeStart, isRangeEnd
    };
  }

  private getDayOverlaps(bookedPeriods: BookedPeriod[], dayStart: Date, dayEnd: Date): { start: Date; end: Date }[] {
    const overlaps: { start: Date; end: Date }[] = [];
    for (const p of bookedPeriods) {
      const pStart = new Date(p.startDate);
      const pEnd = new Date(p.endDate);
      const overlapStart = pStart > dayStart ? pStart : dayStart;
      const overlapEnd = pEnd < dayEnd ? pEnd : dayEnd;
      if (overlapStart < overlapEnd) {
        overlaps.push({ start: overlapStart, end: overlapEnd });
      }
    }
    return overlaps;
  }

  private getBookedIntervalsForDay(bookedPeriods: BookedPeriod[], dayStart: Date, dayEnd: Date): string {
    return this.getDayOverlaps(bookedPeriods, dayStart, dayEnd)
      .map(o => `${this.formatTime(o.start.getHours(), o.start.getMinutes())}-${this.formatTime(o.end.getHours(), o.end.getMinutes())}`)
      .join(', ');
  }

  private getBookedHoursForDay(bookedPeriods: BookedPeriod[], dayStart: Date, dayEnd: Date): number {
    const total = this.getDayOverlaps(bookedPeriods, dayStart, dayEnd)
      .reduce((sum, o) => sum + (o.end.getTime() - o.start.getTime()) / (1000 * 60 * 60), 0);
    return Math.round(total * 10) / 10;
  }

  prevMonth(key: string): void {
    const state = this.calendarStates.get(key);
    if (!state) return;
    state.calendarMonth = new Date(state.calendarMonth.getFullYear(), state.calendarMonth.getMonth() - 1, 1);
    this.buildCalendar(key);
  }

  nextMonth(key: string): void {
    const state = this.calendarStates.get(key);
    if (!state) return;
    state.calendarMonth = new Date(state.calendarMonth.getFullYear(), state.calendarMonth.getMonth() + 1, 1);
    this.buildCalendar(key);
  }

  selectCalendarDay(item: CartItem, day: CalendarDay): void {
    const key = this.itemKey(item);
    const state = this.calendarStates.get(key);
    if (!state || day.isPast || !day.isCurrentMonth) return;

    const clickedDate = this.normalizeToMidnight(day.date);

    if (!state.rangeStartDate || (state.rangeStartDate && state.rangeEndDate)) {
      // Deselect end date
      if (state.rangeStartDate && state.rangeEndDate &&
          clickedDate.getTime() === state.rangeEndDate.getTime()) {
        this.clearRange(key, state, { keepStart: true });
        this.updateItemFromCalendar(item, state);
        this.buildCalendar(key);
        return;
      }
      // Start new range
      this.calendarErrors.delete(key);
      state.rangeStartDate = clickedDate;
      state.rangeEndDate = null;
      state.isCustomDuration = false;
    } else {
      // Deselect start date
      if (clickedDate.getTime() === state.rangeStartDate.getTime()) {
        this.clearRange(key, state);
        this.updateItemFromCalendar(item, state);
        this.buildCalendar(key);
        return;
      }

      // Order start/end
      let start = state.rangeStartDate;
      let end = clickedDate;
      if (clickedDate < state.rangeStartDate) {
        start = clickedDate;
        end = state.rangeStartDate;
      }

      // Overlap check
      const rangeStart = new Date(start);
      rangeStart.setHours(state.selectedHour, 0, 0, 0);
      const rangeEnd = new Date(end);
      rangeEnd.setHours(state.selectedHour, 0, 0, 0);

      const overlapping = this.findOverlappingBooking(state.bookedPeriods, rangeStart, rangeEnd);
      if (overlapping) {
        this.calendarErrors.set(key, `Perioadă ocupată din ${this.formatDateShort(new Date(overlapping.startDate))}`);
        return;
      }

      this.calendarErrors.delete(key);
      state.rangeStartDate = start;
      state.rangeEndDate = end;
    }
    this.updateItemFromCalendar(item, state);
    this.buildCalendar(key);
  }

  private clearRange(key: string, state: ItemCalendar, opts?: { keepStart: boolean }): void {
    this.calendarErrors.delete(key);
    if (!opts?.keepStart) {
      state.rangeStartDate = null;
    }
    state.rangeEndDate = null;
    state.isCustomDuration = false;
  }

  selectDuration(item: CartItem, d: number): void {
    const key = this.itemKey(item);
    const state = this.calendarStates.get(key);
    if (state) {
      state.isCustomDuration = false;
      state.rangeEndDate = null;
      this.buildCalendar(key);
    }
    this.cartService.updateItem(item.spotId, { durationHours: d }, item.pontoonId);
  }

  enableCustomDuration(item: CartItem): void {
    const key = this.itemKey(item);
    const state = this.calendarStates.get(key);
    if (state) {
      state.isCustomDuration = true;
      this.buildCalendar(key);
    }
  }

  setCustomDuration(item: CartItem, hours: number): void {
    const h = Math.max(1, Math.min(8760, Math.round(Number(hours))));
    if (!isFinite(h)) return;
    const key = this.itemKey(item);
    const state = this.calendarStates.get(key);
    if (state) {
      state.isCustomDuration = true;
      state.rangeEndDate = null;
      this.buildCalendar(key);
    }
    this.cartService.updateItem(item.spotId, { durationHours: h }, item.pontoonId);
  }

  private updateItemFromCalendar(item: CartItem, state: ItemCalendar): void {
    if (!state.rangeStartDate) return;
    const d = state.rangeStartDate;
    const selected = new Date(d.getFullYear(), d.getMonth(), d.getDate(), state.selectedHour, 0);
    const offset = selected.getTimezoneOffset();
    const local = new Date(selected.getTime() - offset * 60000);
    const startDate = local.toISOString().slice(0, 16);
    let durationHours = this.cartService.items().find(
      i => i.spotId === item.spotId && i.pontoonId === item.pontoonId
    )?.durationHours ?? 24;
    if (state.rangeEndDate) {
      const start = new Date(state.rangeStartDate.getFullYear(), state.rangeStartDate.getMonth(), state.rangeStartDate.getDate(), state.selectedHour, 0);
      const end = new Date(state.rangeEndDate.getFullYear(), state.rangeEndDate.getMonth(), state.rangeEndDate.getDate(), state.selectedHour, 0);
      durationHours = Math.max(24, Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60)));
      state.isCustomDuration = true;
    }
    this.cartService.updateItem(item.spotId, { startDate, durationHours }, item.pontoonId);
  }

  incrementHour(item: CartItem): void {
    const state = this.calendarStates.get(this.itemKey(item));
    if (!state) return;
    state.selectedHour = (state.selectedHour + 1) % 24;
    this.updateItemFromCalendar(item, state);
  }

  decrementHour(item: CartItem): void {
    const state = this.calendarStates.get(this.itemKey(item));
    if (!state) return;
    state.selectedHour = (state.selectedHour - 1 + 24) % 24;
    this.updateItemFromCalendar(item, state);
  }

  onTimeWheel(event: WheelEvent, item: CartItem): void {
    event.preventDefault();
    if (event.deltaY < 0) this.incrementHour(item);
    else this.decrementHour(item);
  }

  calendarMonthLabel(key: string): string {
    const state = this.calendarStates.get(key);
    if (!state) return '';
    return state.calendarMonth.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
  }

  rangeLabel(key: string): string {
    const state = this.calendarStates.get(key);
    if (!state || !state.rangeStartDate) return 'Select dates on calendar';
    if (!state.rangeEndDate) return this.formatDateShort(state.rangeStartDate);
    return `${this.formatDateShort(state.rangeStartDate)} → ${this.formatDateShort(state.rangeEndDate)}`;
  }

  formattedHour(key: string): string {
    const state = this.calendarStates.get(key);
    if (!state) return '08:00';
    return this.formatTime(state.selectedHour);
  }

  calendarError(key: string): string {
    return this.calendarErrors.get(key) ?? '';
  }

  bookingStartLabel(item: CartItem): string {
    const state = this.calendarStates.get(this.itemKey(item));
    if (!state?.rangeStartDate) return '—';
    return `${this.formatDateFull(state.rangeStartDate)} · ${this.formatTime(state.selectedHour)}`;
  }

  bookingEndLabel(item: CartItem): string {
    if (!item.startDate) return '—';
    const end = new Date(new Date(item.startDate).getTime() + item.durationHours * 3600000);
    return `${this.formatDateFull(end)} · ${this.formatTime(end.getHours(), end.getMinutes())}`;
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
        this.showToast('Set the start date for all bookings', 'error');
        return;
      }

      if (this.itemTotal(item) <= 0) {
        const itemName = item.pontoonName ? `${item.spotName} - ${item.pontoonName}` : item.spotName;
        this.showToast(`Booking "${itemName}" has invalid price (0 RON). Update the spot price before checkout.`, 'error');
        return;
      }

      if (new Date(item.startDate) < new Date(now.getTime() - 5 * 60 * 1000)) {
        const itemName = item.pontoonName ? `${item.spotName} - ${item.pontoonName}` : item.spotName;
        this.showToast(`Start date for "${itemName}" cannot be in the past`, 'error');
        return;
      }
    }

    if (this.cardValidationError) {
      this.showToast('Card details are not valid.', 'error');
      return;
    }

    if (!this.stripeReady || !this.stripe || !this.cardElement) {
      await this.mountCardElement();
    }

    if (!this.stripeReady || !this.stripe || !this.cardElement) {
      this.showToast(this.stripeError || 'Payment form is not available.', 'error');
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
          throw new Error(paymentResult.error.message ?? 'Payment failed.');
        }

        const { paymentIntent } = paymentResult;
        if (!paymentIntent?.id || paymentIntent.status !== 'succeeded') {
          throw new Error('Payment was not confirmed by Stripe.');
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
      this.showToast('All bookings have been confirmed and paid!', 'success');
      setTimeout(() => this.router.navigate(['/my-bookings']), 1500);
      return;
    }

    if (completed === 0) {
      this.showToast(firstError || 'Checkout failed.', 'error');
      return;
    }

    this.showToast(
      `${completed} booking(s) confirmed, ${errors} failed${firstError ? `: ${firstError}` : ''}`,
      'error'
    );
    setTimeout(() => this.router.navigate(['/my-bookings']), 2000);
  }

  private formatDateShort(d: Date): string {
    return d.toLocaleDateString('en-US', { day: 'numeric', month: 'short' });
  }

  private formatDateFull(d: Date): string {
    return d.toLocaleDateString('en-US', { day: 'numeric', month: 'short', year: 'numeric' });
  }

  private normalizeToMidnight(d: Date): Date {
    return new Date(d.getFullYear(), d.getMonth(), d.getDate());
  }

  private formatTime(hours: number, minutes = 0): string {
    return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
  }

  private findOverlappingBooking(periods: BookedPeriod[], from: Date, to: Date): BookedPeriod | undefined {
    return periods.find(p => {
      const pStart = new Date(p.startDate);
      const pEnd = new Date(p.endDate);
      return from < pEnd && to > pStart;
    });
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const payload = error.error ?? error.message ?? 'Unknown error';
      return typeof payload === 'string' ? payload : JSON.stringify(payload);
    }

    if (error instanceof Error) {
      return error.message;
    }

    return 'Unknown error';
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
