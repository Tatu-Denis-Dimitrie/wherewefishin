import { Component, OnInit, OnDestroy, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FishingSpotService } from '../../services/fishing-spot.service';
import { FishingSpot } from '../../models/fishing-spot.model';
import { CartService } from '../../services/cart.service';
import { ReviewService, Review, ReviewStats } from '../../services/review.service';
import { PontoonService, Pontoon } from '../../services/pontoon.service';
import { BookingService } from '../../services/booking.service';
import { StockingService } from '../../services/stocking.service';
import { BookedPeriod } from '../../models/booking.model';
import { FishStocking } from '../../models/stocking.model';
import { AuthService } from '../../services/auth.service';
import { AppIcon } from '../../shared/icons/app-icon';
import * as L from 'leaflet';

@Component({
  selector: 'app-fishing-spot-detail',
  imports: [CommonModule, FormsModule, RouterModule, AppIcon],
  templateUrl: './fishing-spot-detail.html',
  styleUrl: './fishing-spot-detail.css',
  encapsulation: ViewEncapsulation.None
})
export class FishingSpotDetail implements OnInit, OnDestroy {
  spot: FishingSpot | null = null;
  loading = true;
  notFound = false;

  // Booking form
  startDate = '';
  durationHours = 24;
  readonly DURATIONS = [12, 24, 48, 72];
  isCustomDuration = false;

  showMessage = '';
  messageType: 'success' | 'error' = 'success';

  // Reviews
  reviews: Review[] = [];
  reviewStats: ReviewStats | null = null;
  loadingReviews = false;
  newReviewRating = 5;
  newReviewComment = '';
  submittingReview = false;
  userHasReviewed = false;
  editingReviewId: number | null = null;

  // Pontoons
  pontoons: Pontoon[] = [];
  selectedPontoonId: number | null = null;

  // Fish species
  fishSpecies: string[] = [];

  // Fish stocking
  stockings: FishStocking[] = [];

  // Calendar
  calendarMonth = new Date();
  calendarDays: { date: Date; dayNum: number; isCurrentMonth: boolean; isToday: boolean; isBooked: boolean; isPartiallyBooked: boolean; bookedHours: number; bookedIntervals: string; isPast: boolean; isSelected: boolean; isInRange: boolean; isRangeStart: boolean; isRangeEnd: boolean }[] = [];
  bookedPeriods: BookedPeriod[] = [];
  selectedHour = 8;
  rangeStartDate: Date | null = null;
  rangeEndDate: Date | null = null;
  calendarError = '';

  private map: L.Map | null = null;
  private pontoonLayers: Map<number, L.Polygon | L.Rectangle> = new Map();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fishingSpotService: FishingSpotService,
    public cartService: CartService,
    private reviewService: ReviewService,
    private pontoonService: PontoonService,
    private bookingService: BookingService,
    private stockingService: StockingService,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    const defaultStart = new Date();
    defaultStart.setHours(defaultStart.getHours() + 1, 0, 0, 0);
    this.selectedHour = defaultStart.getHours();
    defaultStart.setMinutes(defaultStart.getMinutes() - defaultStart.getTimezoneOffset());
    this.startDate = defaultStart.toISOString().slice(0, 16);
    this.calendarMonth = new Date(defaultStart.getFullYear(), defaultStart.getMonth(), 1);
    this.buildCalendar();

    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.fishingSpotService.getById(id).subscribe({
      next: (spot) => {
        this.spot = spot;
        this.loading = false;
        this.loadReviews(id);
        this.loadPontoons(id);
        this.loadFishSpecies();
        this.loadStockings(id);
        setTimeout(() => this.initMap(), 100);
      },
      error: () => {
        this.loading = false;
        this.notFound = true;
      }
    });
  }

  private loadReviews(spotId: number): void {
    this.loadingReviews = true;
    this.reviewService.getSpotReviews(spotId).subscribe({
      next: (reviews) => {
        this.reviews = reviews;
        this.loadingReviews = false;
        const userId = this.authService.getUserId();
        this.userHasReviewed = reviews.some(r => r.userId === userId);
      },
      error: () => {
        this.loadingReviews = false;
      }
    });

    this.reviewService.getAverageRating(spotId).subscribe({
      next: (stats) => {
        this.reviewStats = stats;
      }
    });
  }

  private loadPontoons(spotId: number): void {
    this.pontoonService.getSpotPontoons(spotId).subscribe({
      next: (pontoons) => {
        this.pontoons = pontoons;
        // Auto-select first pontoon if available
        if (pontoons.length > 0) {
          this.selectedPontoonId = pontoons[0].id;
        }
        this.renderPontoonsOnMap();
        this.loadBookedPeriods();
      }
    });
  }

  private renderPontoonsOnMap(): void {
    if (!this.map) return;
    
    // Remove existing pontoon layers
    this.pontoonLayers.forEach(layer => layer.remove());
    this.pontoonLayers.clear();

    // Add pontoons as polygons or rectangles (backward compat)
    this.pontoons.forEach(pontoon => {
      let layer: L.Polygon | L.Rectangle;

      if (pontoon.coordinates) {
        const coords: [number, number][] = JSON.parse(pontoon.coordinates);
        layer = L.polygon(coords.map(c => L.latLng(c[0], c[1])), this.getPontoonLayerStyle(pontoon.id, pontoon.color)).addTo(this.map!);
      } else {
        const bounds: L.LatLngBoundsExpression = [
          [pontoon.southWestLat, pontoon.southWestLng],
          [pontoon.northEastLat, pontoon.northEastLng]
        ];
        layer = L.rectangle(bounds, this.getPontoonLayerStyle(pontoon.id, pontoon.color)).addTo(this.map!);
      }

      layer.bindTooltip(pontoon.name, { permanent: false, direction: 'center' });
      layer.on('click', () => this.selectPontoon(pontoon.id));
      this.pontoonLayers.set(pontoon.id, layer);
    });

    this.updatePontoonLayerStyles();
  }

  selectPontoon(pontoonId: number): void {
    this.selectedPontoonId = pontoonId;
    this.updatePontoonLayerStyles();
    this.loadBookedPeriods();
  }

  private getPontoonLayerStyle(pontoonId: number, color?: string): L.PathOptions {
    const baseColor = color || '#3388ff';
    const isSelected = this.selectedPontoonId === pontoonId;

    return {
      color: baseColor,
      fillColor: baseColor,
      weight: isSelected ? 3 : 2,
      opacity: isSelected ? 1 : 0.85,
      fillOpacity: isSelected ? 0.45 : 0.28
    };
  }

  private updatePontoonLayerStyles(): void {
    this.pontoons.forEach(pontoon => {
      const layer = this.pontoonLayers.get(pontoon.id);
      if (!layer) return;

      layer.setStyle(this.getPontoonLayerStyle(pontoon.id, pontoon.color));
      if (this.selectedPontoonId === pontoon.id) {
        layer.bringToFront();
      }
    });
  }

  // ---- Calendar methods ----
  loadBookedPeriods(): void {
    if (!this.spot) return;
    const obs = this.selectedPontoonId
      ? this.bookingService.getBookedPeriods(this.selectedPontoonId)
      : this.bookingService.getBookedPeriods(undefined, this.spot.id);
    obs.subscribe({
      next: (periods) => {
        this.bookedPeriods = periods;
        this.buildCalendar();
      },
      error: () => {
        this.bookedPeriods = [];
        this.buildCalendar();
      }
    });
  }

  private loadFishSpecies(): void {
    this.fishSpecies = this.spot?.fishSpecies ? JSON.parse(this.spot.fishSpecies) : [];
  }

  private loadStockings(spotId: number): void {
    this.stockingService.getStockings(spotId).subscribe({
      next: (stockings) => this.stockings = stockings,
      error: () => this.stockings = []
    });
  }

  buildCalendar(): void {
    const year = this.calendarMonth.getFullYear();
    const month = this.calendarMonth.getMonth();
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const rangeStart = this.rangeStartDate ? new Date(this.rangeStartDate) : null;
    const rangeEnd = this.rangeEndDate ? new Date(this.rangeEndDate) : null;
    if (rangeStart) rangeStart.setHours(0, 0, 0, 0);
    if (rangeEnd) rangeEnd.setHours(0, 0, 0, 0);

    const days: typeof this.calendarDays = [];

    // Fill leading days from prev month
    const startWeekday = (firstDay.getDay() + 6) % 7;
    for (let i = startWeekday - 1; i >= 0; i--) {
      const d = new Date(year, month, -i);
      days.push(this.makeCalendarDay(d, false, today, rangeStart, rangeEnd));
    }

    // Current month days
    for (let d = 1; d <= lastDay.getDate(); d++) {
      const date = new Date(year, month, d);
      days.push(this.makeCalendarDay(date, true, today, rangeStart, rangeEnd));
    }

    // Fill trailing days
    const totalCells = Math.ceil(days.length / 7) * 7;
    let nextDay = 1;
    while (days.length < totalCells) {
      const d = new Date(year, month + 1, nextDay++);
      days.push(this.makeCalendarDay(d, false, today, rangeStart, rangeEnd));
    }

    this.calendarDays = days;
  }

  private makeCalendarDay(date: Date, isCurrentMonth: boolean, today: Date, rangeStart: Date | null, rangeEnd: Date | null) {
    const dateMidnight = new Date(date);
    dateMidnight.setHours(0, 0, 0, 0);
    const dateEnd = new Date(dateMidnight);
    dateEnd.setHours(23, 59, 59, 999);

    const bookedHours = this.getBookedHoursForDay(dateMidnight, dateEnd);
    const isFullyBooked = bookedHours >= 24;
    const isPartiallyBooked = bookedHours > 0 && bookedHours < 24;
    const bookedIntervals = isPartiallyBooked || isFullyBooked ? this.getBookedIntervalsForDay(dateMidnight, dateEnd) : '';

    const isRangeStart = rangeStart ? dateMidnight.getTime() === rangeStart.getTime() : false;
    const isRangeEnd = rangeEnd ? dateMidnight.getTime() === rangeEnd.getTime() : false;
    const isInRange = rangeStart && rangeEnd
      ? dateMidnight >= rangeStart && dateMidnight <= rangeEnd
      : isRangeStart;

    return {
      date,
      dayNum: date.getDate(),
      isCurrentMonth,
      isToday: dateMidnight.getTime() === today.getTime(),
      isBooked: isFullyBooked,
      isPartiallyBooked,
      bookedHours,
      bookedIntervals,
      isPast: dateMidnight < today,
      isSelected: isRangeStart || isRangeEnd,
      isInRange,
      isRangeStart,
      isRangeEnd
    };
  }

  private getBookedIntervalsForDay(dayStart: Date, dayEnd: Date): string {
    const intervals: string[] = [];
    for (const p of this.bookedPeriods) {
      const pStart = new Date(p.startDate);
      const pEnd = new Date(p.endDate);
      const overlapStart = pStart > dayStart ? pStart : dayStart;
      const overlapEnd = pEnd < dayEnd ? pEnd : dayEnd;
      if (overlapStart < overlapEnd) {
        const sh = overlapStart.getHours().toString().padStart(2, '0');
        const sm = overlapStart.getMinutes().toString().padStart(2, '0');
        const eh = overlapEnd.getHours().toString().padStart(2, '0');
        const em = overlapEnd.getMinutes().toString().padStart(2, '0');
        intervals.push(`${sh}:${sm}-${eh}:${em}`);
      }
    }
    return intervals.join(', ');
  }

  private getBookedHoursForDay(dayStart: Date, dayEnd: Date): number {
    let totalHours = 0;
    for (const p of this.bookedPeriods) {
      const pStart = new Date(p.startDate);
      const pEnd = new Date(p.endDate);
      const overlapStart = pStart > dayStart ? pStart : dayStart;
      const overlapEnd = pEnd < dayEnd ? pEnd : dayEnd;
      if (overlapStart < overlapEnd) {
        totalHours += (overlapEnd.getTime() - overlapStart.getTime()) / (1000 * 60 * 60);
      }
    }
    return Math.round(totalHours * 10) / 10;
  }

  private isDayBooked(dayStart: Date, dayEnd: Date): boolean {
    return this.bookedPeriods.some(p => {
      const pStart = new Date(p.startDate);
      const pEnd = new Date(p.endDate);
      return pStart < dayEnd && pEnd > dayStart;
    });
  }

  private findOverlappingBooking(periods: BookedPeriod[], from: Date, to: Date): BookedPeriod | undefined {
    return periods.find(p => {
      const pStart = new Date(p.startDate);
      const pEnd = new Date(p.endDate);
      return from < pEnd && to > pStart;
    });
  }

  private formatDateShort(d: Date): string {
    return d.toLocaleDateString('en-US', { day: 'numeric', month: 'short' });
  }

  prevMonth(): void {
    this.calendarMonth = new Date(
      this.calendarMonth.getFullYear(),
      this.calendarMonth.getMonth() - 1,
      1
    );
    this.buildCalendar();
  }

  nextMonth(): void {
    this.calendarMonth = new Date(
      this.calendarMonth.getFullYear(),
      this.calendarMonth.getMonth() + 1,
      1
    );
    this.buildCalendar();
  }

  selectCalendarDay(day: typeof this.calendarDays[0]): void {
    if (day.isPast || !day.isCurrentMonth) return;
    const clickedDate = new Date(day.date.getFullYear(), day.date.getMonth(), day.date.getDate());

    if (!this.rangeStartDate || (this.rangeStartDate && this.rangeEndDate)) {
      // Deselect end date on re-click
      if (this.rangeStartDate && this.rangeEndDate &&
          clickedDate.getTime() === this.rangeEndDate.getTime()) {
        this.calendarError = '';
        this.rangeEndDate = null;
        this.isCustomDuration = false;
        this.updateStartDateFromRange();
        this.buildCalendar();
        return;
      }
      // Start new range
      this.calendarError = '';
      this.rangeStartDate = clickedDate;
      this.rangeEndDate = null;
      this.isCustomDuration = false;
    } else {
      // Deselect start date on re-click
      if (clickedDate.getTime() === this.rangeStartDate.getTime()) {
        this.calendarError = '';
        this.rangeStartDate = null;
        this.rangeEndDate = null;
        this.isCustomDuration = false;
        this.updateStartDateFromRange();
        this.buildCalendar();
        return;
      }

      // Order start/end
      let start = this.rangeStartDate;
      let end = clickedDate;
      if (clickedDate < this.rangeStartDate) {
        start = clickedDate;
        end = this.rangeStartDate;
      }

      // Overlap check
      const rangeStart = new Date(start);
      rangeStart.setHours(this.selectedHour, 0, 0, 0);
      const rangeEnd = new Date(end);
      rangeEnd.setHours(this.selectedHour, 0, 0, 0);

      const overlapping = this.findOverlappingBooking(this.bookedPeriods, rangeStart, rangeEnd);
      if (overlapping) {
        this.calendarError = `Perioadă ocupată din ${this.formatDateShort(new Date(overlapping.startDate))}`;
        return;
      }

      this.calendarError = '';
      this.rangeStartDate = start;
      this.rangeEndDate = end;
    }

    this.updateStartDateFromRange();
    this.buildCalendar();
  }

  private updateStartDateFromRange(): void {
    if (!this.rangeStartDate) return;
    const d = this.rangeStartDate;
    const selected = new Date(d.getFullYear(), d.getMonth(), d.getDate(), this.selectedHour, 0);
    const offset = selected.getTimezoneOffset();
    const local = new Date(selected.getTime() - offset * 60000);
    this.startDate = local.toISOString().slice(0, 16);

    if (this.rangeEndDate) {
      // Calculate total hours from start to end+selectedHour
      const start = new Date(this.rangeStartDate.getFullYear(), this.rangeStartDate.getMonth(), this.rangeStartDate.getDate(), this.selectedHour, 0);
      const end = new Date(this.rangeEndDate.getFullYear(), this.rangeEndDate.getMonth(), this.rangeEndDate.getDate(), this.selectedHour, 0);
      const diffMs = end.getTime() - start.getTime();
      const diffHours = Math.max(24, Math.round(diffMs / (1000 * 60 * 60)));
      this.durationHours = diffHours;
      this.isCustomDuration = true;
    }
  }

  incrementHour(): void {
    this.selectedHour = (this.selectedHour + 1) % 24;
    this.updateStartDateFromRange();
  }

  decrementHour(): void {
    this.selectedHour = (this.selectedHour - 1 + 24) % 24;
    this.updateStartDateFromRange();
  }

  // Scroll wheel on time spinner
  onTimeWheel(event: WheelEvent): void {
    event.preventDefault();
    if (event.deltaY < 0) this.incrementHour();
    else if (event.deltaY > 0) this.decrementHour();
  }

  // Touch swipe on time spinner
  private timeTouchStartY = 0;
  private timeTouchAccum = 0;

  onTimeTouchStart(event: TouchEvent): void {
    this.timeTouchStartY = event.touches[0].clientY;
    this.timeTouchAccum = 0;
  }

  onTimeTouchMove(event: TouchEvent): void {
    event.preventDefault();
    const dy = this.timeTouchStartY - event.touches[0].clientY;
    this.timeTouchAccum += dy;
    this.timeTouchStartY = event.touches[0].clientY;
    const threshold = 30;
    while (this.timeTouchAccum >= threshold) {
      this.incrementHour();
      this.timeTouchAccum -= threshold;
    }
    while (this.timeTouchAccum <= -threshold) {
      this.decrementHour();
      this.timeTouchAccum += threshold;
    }
  }

  onTimeTouchEnd(): void {
    this.timeTouchAccum = 0;
  }

  get formattedHour(): string {
    return `${String(this.selectedHour).padStart(2, '0')}:00`;
  }

  selectDuration(d: number): void {
    this.durationHours = d;
    this.isCustomDuration = false;
    // Clear range end when picking preset duration
    this.rangeEndDate = null;
    this.buildCalendar();
  }

  get calendarMonthLabel(): string {
    return this.calendarMonth.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
  }

  get rangeLabel(): string {
    if (!this.rangeStartDate) return 'Select dates on calendar';
    const fmt = (d: Date) => d.toLocaleDateString('en-US', { day: 'numeric', month: 'short' });
    if (!this.rangeEndDate) return fmt(this.rangeStartDate);
    return `${fmt(this.rangeStartDate)} → ${fmt(this.rangeEndDate)}`;
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.remove();
      this.map = null;
    }
  }

  private initMap(): void {
    if (!this.spot) return;
    const el = document.getElementById('spot-mini-map');
    if (!el) return;

    const centerLat = this.spot.defaultCenterLat ?? this.spot.latitude;
    const centerLng = this.spot.defaultCenterLng ?? this.spot.longitude;
    const zoom = this.spot.defaultZoom ?? 16;

    this.map = L.map('spot-mini-map', {
      zoomControl: false,
      scrollWheelZoom: false,
      dragging: false,
      touchZoom: false,
      doubleClickZoom: false,
      boxZoom: false,
      keyboard: false,
      attributionControl: false
    }).setView([centerLat, centerLng], zoom);

    L.tileLayer('https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}', {
      attribution: '© Google Maps',
      maxZoom: 22
    }).addTo(this.map);

    const icon = L.divIcon({
      html: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 68" width="36" height="51">
        <ellipse cx="24" cy="64" rx="8" ry="4" fill="rgba(0,0,0,0.25)"/>
        <path d="M24 2C13.5 2 5 10.5 5 21c0 14 19 43 19 43S43 35 43 21C43 10.5 34.5 2 24 2z" fill="#7db34a" stroke="#fff" stroke-width="2"/>
        <path d="M24 22 q-7-5-7-12 c0-1.5 0.3-3 0.8-4.3 C16 7.8 14 10.5 14 13.5c0 5.5 4.5 10 10 10s10-4.5 10-10c0-3-1.3-5.7-3.3-7.5C31.2 7.5 31 9 31 10.5c0 6.3-5 11-7 11.5z" fill="rgba(255,255,255,0.3)"/>
        <circle cx="24" cy="21" r="7" fill="rgba(255,255,255,0.9)"/>
        <path d="M24 19 q-1-1.5-1-3.5 q1 2 2.5 2 q-0.5 0.5-1.5 0.5 q0.5 1 1.5 1.5 q-1.5 0 -1.5 1.5z" fill="#5a9a35"/>
      </svg>`,
      className: '',
      iconSize: [36, 51],
      iconAnchor: [18, 51],
    });

    L.marker([this.spot.latitude, this.spot.longitude], { icon }).addTo(this.map!);
    
    // Render pontoons after map is ready
    setTimeout(() => this.renderPontoonsOnMap(), 100);
  }

  // Review methods
  submitReview(): void {
    if (!this.spot || !this.authService.isLoggedIn()) return;
    
    this.submittingReview = true;
    this.reviewService.createReview({
      fishingSpotId: this.spot.id,
      rating: this.newReviewRating,
      comment: this.newReviewComment.trim() || undefined
    }).subscribe({
      next: () => {
        this.showToast('Review added successfully!', 'success');
        this.newReviewComment = '';
        this.newReviewRating = 5;
        this.submittingReview = false;
        this.loadReviews(this.spot!.id);
      },
      error: (err) => {
        const msg = err.error?.message || err.error || 'Error adding review';
        this.showToast(msg, 'error');
        this.submittingReview = false;
      }
    });
  }

  startEditReview(review: Review): void {
    this.editingReviewId = review.id;
    this.newReviewRating = review.rating;
    this.newReviewComment = review.comment || '';
  }

  cancelEditReview(): void {
    this.editingReviewId = null;
    this.newReviewRating = 5;
    this.newReviewComment = '';
  }

  updateReview(): void {
    if (!this.editingReviewId) return;
    
    this.submittingReview = true;
    this.reviewService.updateReview(this.editingReviewId, {
      rating: this.newReviewRating,
      comment: this.newReviewComment.trim() || undefined
    }, this.spot!.id).subscribe({
      next: () => {
        this.showToast('Review updated!', 'success');
        this.editingReviewId = null;
        this.newReviewComment = '';
        this.newReviewRating = 5;
        this.submittingReview = false;
        this.loadReviews(this.spot!.id);
      },
      error: () => {
        this.showToast('Error updating review', 'error');
        this.submittingReview = false;
      }
    });
  }

  deleteReview(reviewId: number): void {
    if (!confirm('Are you sure you want to delete this review?')) return;
    
    this.reviewService.deleteReview(reviewId, this.spot!.id).subscribe({
      next: () => {
        this.showToast('Review deleted!', 'success');
        this.loadReviews(this.spot!.id);
      },
      error: () => {
        this.showToast('Error deleting review', 'error');
      }
    });
  }

  canEditReview(review: Review): boolean {
    const userId = this.authService.getUserId();
    return review.userId === userId || this.authService.isAdmin();
  }

  canManageSpot(): boolean {
    if (!this.spot) return false;
    const userId = this.authService.getUserId();
    return this.authService.isAdmin() || this.spot.managerId === userId || this.spot.userId === userId;
  }

  setRating(rating: number): void {
    this.newReviewRating = rating;
  }

  get totalPrice(): number {
    if (!this.spot) return 0;
    return this.spot.pricePerHour * this.durationHours;
  }

  get selectedPontoon(): Pontoon | null {
    return this.pontoons.find(p => p.id === this.selectedPontoonId) || null;
  }

  get inCart(): boolean {
    if (!this.spot) return false;
    // If spot has pontoons, check if selected pontoon is in cart
    if (this.pontoons.length > 0 && this.selectedPontoonId) {
      return this.cartService.isInCart(this.spot.id, this.selectedPontoonId);
    }
    // Otherwise check if spot itself is in cart (no pontoons)
    return this.cartService.isInCart(this.spot.id);
  }

  addToCart(): void {
    if (!this.spot) return;
    if (this.inCart) {
      this.router.navigate(['/cart']);
      return;
    }

    // If spot has pontoons, require a selected pontoon
    if (this.pontoons.length > 0 && !this.selectedPontoonId) {
      this.showToast('Select a pontoon to book', 'error');
      return;
    }

    const pontoon = this.selectedPontoon;
    this.cartService.addItem({
      spotId: this.spot.id,
      spotName: this.spot.name,
      pontoonId: pontoon?.id,
      pontoonName: pontoon?.name,
      latitude: this.spot.latitude,
      longitude: this.spot.longitude,
      pricePerHour: this.spot.pricePerHour,
      durationHours: this.durationHours,
      startDate: this.startDate
    });
    
    const itemName = pontoon ? `${this.spot.name} - ${pontoon.name}` : this.spot.name;
    this.showToast(`"${itemName}" added to cart!`, 'success');
  }

  goBack(): void {
    this.router.navigate(['/home']);
  }

  private showToast(msg: string, type: 'success' | 'error'): void {
    this.showMessage = msg;
    this.messageType = type;
    setTimeout(() => this.showMessage = '', 3000);
  }
}
