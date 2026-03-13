import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FishingSpotService, FishingSpot } from '../../services/fishing-spot.service';
import { CartService } from '../../services/cart.service';
import { ReviewService, Review, ReviewStats } from '../../services/review.service';
import { PontoonService, Pontoon } from '../../services/pontoon.service';
import { AuthService } from '../../services/auth.service';
import * as L from 'leaflet';

@Component({
  selector: 'app-fishing-spot-detail',
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './fishing-spot-detail.html',
  styleUrl: './fishing-spot-detail.css'
})
export class FishingSpotDetail implements OnInit, OnDestroy {
  spot: FishingSpot | null = null;
  loading = true;
  notFound = false;

  // Booking form
  startDate = '';
  durationHours = 24;
  readonly DURATIONS = [12, 24, 48, 72];

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

  private map: L.Map | null = null;
  private pontoonLayers: L.Rectangle[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fishingSpotService: FishingSpotService,
    public cartService: CartService,
    private reviewService: ReviewService,
    private pontoonService: PontoonService,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    const defaultStart = new Date();
    defaultStart.setHours(defaultStart.getHours() + 1, 0, 0, 0);
    defaultStart.setMinutes(defaultStart.getMinutes() - defaultStart.getTimezoneOffset());
    this.startDate = defaultStart.toISOString().slice(0, 16);

    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.fishingSpotService.getById(id).subscribe({
      next: (spot) => {
        this.spot = spot;
        this.loading = false;
        this.loadReviews(id);
        this.loadPontoons(id);
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
      }
    });
  }

  private renderPontoonsOnMap(): void {
    if (!this.map) return;
    
    // Remove existing pontoon layers
    this.pontoonLayers.forEach(layer => layer.remove());
    this.pontoonLayers = [];

    // Add pontoons as rectangles
    this.pontoons.forEach(pontoon => {
      const bounds: L.LatLngBoundsExpression = [
        [pontoon.southWestLat, pontoon.southWestLng],
        [pontoon.northEastLat, pontoon.northEastLng]
      ];
      const rect = L.rectangle(bounds, {
        color: pontoon.color || '#3388ff',
        weight: 2,
        fillOpacity: 0.3
      }).addTo(this.map!);
      rect.bindTooltip(pontoon.name, { permanent: false, direction: 'center' });
      this.pontoonLayers.push(rect);
    });
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

    this.map = L.map('spot-mini-map', {
      zoomControl: false,
      scrollWheelZoom: false,
      dragging: false,
      touchZoom: false,
      doubleClickZoom: false,
      boxZoom: false,
      keyboard: false,
      attributionControl: false
    }).setView([this.spot.latitude, this.spot.longitude], 16);

    L.tileLayer('https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}', {
      attribution: '© Google Maps',
      maxZoom: 20
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
        this.showToast('Recenzie adăugată cu succes!', 'success');
        this.newReviewComment = '';
        this.newReviewRating = 5;
        this.submittingReview = false;
        this.loadReviews(this.spot!.id);
      },
      error: (err) => {
        const msg = err.error?.message || err.error || 'Eroare la adăugarea recenziei';
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
    }).subscribe({
      next: () => {
        this.showToast('Recenzie actualizată!', 'success');
        this.editingReviewId = null;
        this.newReviewComment = '';
        this.newReviewRating = 5;
        this.submittingReview = false;
        this.loadReviews(this.spot!.id);
      },
      error: () => {
        this.showToast('Eroare la actualizarea recenziei', 'error');
        this.submittingReview = false;
      }
    });
  }

  deleteReview(reviewId: number): void {
    if (!confirm('Sigur dorești să ștergi această recenzie?')) return;
    
    this.reviewService.deleteReview(reviewId).subscribe({
      next: () => {
        this.showToast('Recenzie ștearsă!', 'success');
        this.loadReviews(this.spot!.id);
      },
      error: () => {
        this.showToast('Eroare la ștergerea recenziei', 'error');
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
      this.showToast('Selectează un ponton pentru rezervare', 'error');
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
    this.showToast(`"${itemName}" adăugat în coș!`, 'success');
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
