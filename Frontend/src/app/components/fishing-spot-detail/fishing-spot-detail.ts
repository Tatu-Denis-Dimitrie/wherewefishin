import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FishingSpotService, FishingSpot } from '../../services/fishing-spot.service';
import { CartService } from '../../services/cart.service';
import * as L from 'leaflet';

@Component({
  selector: 'app-fishing-spot-detail',
  imports: [CommonModule, FormsModule],
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

  private map: L.Map | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fishingSpotService: FishingSpotService,
    public cartService: CartService
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
        setTimeout(() => this.initMap(), 100);
      },
      error: () => {
        this.loading = false;
        this.notFound = true;
      }
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
  }

  get totalPrice(): number {
    if (!this.spot) return 0;
    return this.spot.pricePerHour * this.durationHours;
  }

  get inCart(): boolean {
    return this.spot ? this.cartService.isInCart(this.spot.id) : false;
  }

  addToCart(): void {
    if (!this.spot) return;
    if (this.inCart) {
      this.router.navigate(['/cart']);
      return;
    }
    this.cartService.addItem({
      spotId: this.spot.id,
      spotName: this.spot.name,
      latitude: this.spot.latitude,
      longitude: this.spot.longitude,
      pricePerHour: this.spot.pricePerHour,
      durationHours: this.durationHours,
      startDate: this.startDate
    });
    this.showToast(`"${this.spot.name}" adăugat în coș!`, 'success');
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
