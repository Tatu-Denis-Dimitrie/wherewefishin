import { Component, OnInit, AfterViewInit, OnDestroy, HostListener, ChangeDetectorRef, NgZone, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { FishingSpotService, FishingSpot, CreateFishingSpot } from '../../services/fishing-spot.service';
import { AdminService, AdminStats } from '../../services/admin.service';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { BookingService } from '../../services/booking.service';
import { Booking } from '../../models/booking.model';
import { UserService } from '../../services/user.service';
import { User } from '../../models/user.model';
import * as L from 'leaflet';

interface NearbySpot {
  spot: HomeSpot;
  distanceMeters: number;
  distanceLabel: string;
}

interface HomeSpot extends FishingSpot {
  parsedFishSpecies: string[];
}

@Component({
  selector: 'app-home',
  imports: [CommonModule, FormsModule],
  templateUrl: './home.html',
  styleUrl: './home.css',
  encapsulation: ViewEncapsulation.None
})
export class Home implements OnInit, AfterViewInit, OnDestroy {
  private map!: L.Map;
  private markersLayer!: L.LayerGroup;
  private markerSpotMap = new Map<L.Marker, HomeSpot>();
  private readonly spotSpeciesByName: Record<string, string[]> = {
    'snagov lake': ['Carp', 'Pike', 'Perch'],
    'lacul snagov': ['Carp', 'Pike', 'Perch'],
    'danube delta': ['Pike', 'Catfish', 'Perch'],
    'delta dunarii': ['Pike', 'Catfish', 'Perch'],
    'vidraru dam': ['Trout', 'Perch', 'Carp'],
    'barajul vidraru': ['Trout', 'Perch', 'Carp'],
    'bicaz lake': ['Trout', 'Chub', 'Perch'],
    'lacul bicaz': ['Trout', 'Chub', 'Perch']
  };
  private readonly fallbackSpeciesSets: string[][] = [
    ['Carp', 'Perch'],
    ['Pike', 'Catfish'],
    ['Trout', 'Chub'],
    ['Carp', 'Pike', 'Perch']
  ];
  private userLocationMarker: L.Marker | null = null;
  private userLocationCircle: L.Circle | null = null;
  private userLatLng: L.LatLng | null = null;
  private routeLayer: L.GeoJSON | null = null;
  private readonly userLocationStorageKey = 'wherewefishin.user-location.v1';
  private readonly userLocationMaxAgeMs = 1000 * 60 * 60 * 24 * 7;
  locatingUser = false;
  routeInfo: { distance: string; duration: string; spotName: string } | null = null;

  spots: HomeSpot[] = [];
  visibleSpots: HomeSpot[] = [];
  closestSpots: NearbySpot[] = [];
  fishSpeciesOptions: string[] = [];
  selectedFishSpecies = 'all';
  hasUserLocation = false;
  readonly closestSpotsLimit = 3;
  isAddMode = false;
  isDeleteMode = false;
  showMessage = '';
  messageType: 'success' | 'error' = 'success';
  canEdit = false;
  mapExpanded = false;
  
  // Dashboard data - Admin stats
  stats: AdminStats | null = null;
  
  // User/Manager specific data
  userAnalysesCount = 0;
  userCompletedCount = 0;
  userSpotsCount = 0;
  loadingStats = true;
  loadingLatestSession = true;
  selectedSessionView: 'future' | 'past' = 'future';
  latestPurchasedSession: Booking | null = null;
  latestFutureSession: Booking | null = null;
  latestPastSession: Booking | null = null;
  sessionQrCode = '';
  isSessionQrVisible = false;
  isSessionQrLoading = false;
  private sessionQrBookingId: number | null = null;
  
  // Role helpers
  currentRole: string = '';

  // New spot form
  showSpotForm = false;
  newSpotLat = 0;
  newSpotLng = 0;
  newSpotName = '';
  newSpotDescription = '';
  newSpotPrice = 0;
  newSpotManagerId: number | null = null;
  managers: User[] = [];
  private pendingMarker: L.Marker | null = null;

  constructor(
    private authService: AuthService,
    private fishingSpotService: FishingSpotService,
    private adminService: AdminService,
    private videoAnalysisService: VideoAnalysisService,
    private bookingService: BookingService,
    private router: Router,
    private userService: UserService,
    private cdr: ChangeDetectorRef,
    private zone: NgZone
  ) {}

  ngOnInit(): void {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }
    this.canEdit = this.authService.isManagerOrAdmin();
    this.currentRole = this.authService.getRole() || 'User';
    this.loadDashboardData();
  }

  ngAfterViewInit(): void {
    this.initMap();
    // If forkJoin resolved from cache before ngAfterViewInit (shareReplay sync emit),
    // the markers were skipped because markersLayer didn't exist yet — render them now.
    if (this.spots.length > 0) {
      this.applyFishFilter();
    }
    const restoredFromCache = this.restoreCachedUserLocation();
    if (!restoredFromCache) {
      this.locateUser();
    }
    setTimeout(() => this.map.invalidateSize(), 250);
    this.cdr.detectChanges();
  }

  private loadDashboardData(): void {
    this.loadingStats = true;
    const userId = this.authService.getUserId();

    if (!userId) {
      this.loadingStats = false;
      this.loadingLatestSession = false;
      return;
    }

    forkJoin({
      spots:    this.fishingSpotService.getAll(),
      analyses: this.videoAnalysisService.getUserAnalyses(userId),
      bookings: this.isUser() ? this.bookingService.getMyBookings() : of([] as Booking[]),
      stats:    this.isAdmin() ? this.adminService.getStats() : of(null)
    }).subscribe({
      next: ({ spots, analyses, bookings, stats }) => {
        // Spots — shared with map (replaces separate loadSpots call)
        this.spots = spots.map(s => this.enrichSpotWithSpecies(s));
        this.fishSpeciesOptions = Array.from(
          new Set(this.spots.flatMap(s => s.parsedFishSpecies))
        ).sort((a, b) => a.localeCompare(b));
        if (this.selectedFishSpecies !== 'all' && !this.fishSpeciesOptions.includes(this.selectedFishSpecies)) {
          this.selectedFishSpecies = 'all';
        }
        this.applyFishFilter();

        // Spot count for manager/admin dashboard card
        if (this.isManager() || this.isAdmin()) {
          this.userSpotsCount = spots.filter(s => s.userId === userId).length;
        }

        // Analyses count (all roles)
        this.userAnalysesCount = analyses.length;
        this.userCompletedCount = analyses.filter(a => a.status === 'Completed').length;

        // Bookings (regular users)
        if (this.isUser() && bookings.length > 0) {
          const now = Date.now();
          const active = bookings.filter(b => b.status?.toLowerCase() !== 'cancelled');
          this.latestFutureSession = active
            .filter(b => new Date(b.startDate).getTime() >= now)
            .sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime())[0] ?? null;
          this.latestPastSession = active
            .filter(b => new Date(b.startDate).getTime() < now)
            .sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime())[0] ?? null;
          this.updateSelectedSession();
        }

        // Admin system stats
        if (stats) this.stats = stats;

        this.loadingStats = false;
        this.loadingLatestSession = false;
      },
      error: () => {
        this.loadingStats = false;
        this.loadingLatestSession = false;
      }
    });
  }

  ngOnDestroy(): void {
    if (this.map) this.map.remove();
  }

  private createSpotIcon(): L.DivIcon {
    const fillColor = '#4a7c30';
    return L.divIcon({
      className: '',
      html: `
        <div style="filter:drop-shadow(0 3px 6px rgba(0,0,0,0.45))">
          <svg xmlns="http://www.w3.org/2000/svg" width="34" height="46" viewBox="0 0 34 46">
            <path d="M17 0C7.611 0 0 7.611 0 17c0 11.046 17 29 17 29S34 28.046 34 17C34 7.611 26.389 0 17 0z"
              fill="${fillColor}"/>
            <circle cx="17" cy="16" r="9.5" fill="white" opacity="0.15"/>
            <circle cx="17" cy="16" r="8" fill="white"/>
            <g transform="translate(17,16)">
              <path d="M-5.5 0 C-3.5 -3.5 1 -5 4 0 C1 5 -3.5 3.5 -5.5 0Z"
                fill="${fillColor}" stroke="${fillColor}" stroke-width="0.5"/>
              <path d="M-7.5 -1.5 L-5.5 0 L-7.5 1.5Z"
                fill="${fillColor}"/>
              <circle cx="2" cy="-0.8" r="1.1" fill="white"/>
              <circle cx="2" cy="-0.8" r="0.5" fill="${fillColor}"/>
            </g>
          </svg>
        </div>`,
      iconSize: [34, 46],
      iconAnchor: [17, 46],
      popupAnchor: [0, -48]
    });
  }

  private initMap(): void {
    // Fix Leaflet default marker icon path (broken by bundlers in production)
    delete (L.Icon.Default.prototype as any).options.iconUrl;
    delete (L.Icon.Default.prototype as any).options.iconRetinaUrl;
    delete (L.Icon.Default.prototype as any).options.shadowUrl;
    L.Icon.Default.mergeOptions({
      iconUrl: 'assets/marker-icon.png',
      iconRetinaUrl: 'assets/marker-icon-2x.png',
      shadowUrl: 'assets/marker-shadow.png'
    });

    this.map = L.map('map', {
      center: [45.9432, 24.9668],
      zoom: 8,
      zoomControl: false,
      maxZoom: 20
    });

    L.tileLayer('https://mt1.google.com/vt/lyrs=y&hl=en&x={x}&y={y}&z={z}', {
      attribution: '&copy; <a href="https://maps.google.com">Google Maps</a>',
      maxZoom: 20,
      subdomains: ['mt0', 'mt1', 'mt2', 'mt3']
    }).addTo(this.map);

    this.markersLayer = new L.LayerGroup();
    this.map.addLayer(this.markersLayer);
  }

  private loadSpots(): void {
    this.fishingSpotService.getAll().subscribe({
      next: (spots) => {
        this.spots = spots.map(s => this.enrichSpotWithSpecies(s));
        this.fishSpeciesOptions = Array.from(new Set(this.spots.flatMap(s => s.parsedFishSpecies))).sort((a, b) => a.localeCompare(b));
        if (this.selectedFishSpecies !== 'all' && !this.fishSpeciesOptions.includes(this.selectedFishSpecies)) {
          this.selectedFishSpecies = 'all';
        }
        this.applyFishFilter();
      },
      error: () => this.showToast('Failed to load fishing spots', 'error')
    });
  }

  private renderMarkers(): void {
    if (!this.markersLayer) return;  // map not yet initialized (cache sync emit in ngOnInit)
    this.markersLayer.clearLayers();
    this.markerSpotMap.clear();

    this.visibleSpots.forEach(spot => {
      const marker = L.marker([spot.latitude, spot.longitude], {
        icon: this.createSpotIcon()
      }).bindPopup(this.buildPopupContent(spot), {
        maxWidth: 220,
        autoPanPaddingTopLeft: [16, 96],
        autoPanPaddingBottomRight: [16, 16]
      });

      marker.on('click', () => {
        if (this.isDeleteMode && this.canEdit) {
          this.deleteSpot(spot, marker);
        }
      });

      marker.on('popupopen', (e: any) => {
        const container: HTMLElement | undefined = e.popup.getElement();
        const bookBtn = container?.querySelector<HTMLButtonElement>('.popup-book-btn');
        if (bookBtn) {
          bookBtn.onclick = () => this.router.navigate(['/spots', spot.id]);
        }
        const routeBtn = container?.querySelector<HTMLButtonElement>('.popup-route-btn');
        if (routeBtn) {
          routeBtn.onclick = () => this.showRouteOnMap(spot);
        }
      });

      this.markerSpotMap.set(marker, spot);
      this.markersLayer.addLayer(marker);
    });
  }

  private buildPopupContent(spot: HomeSpot): string {
    const priceHtml = spot.pricePerHour > 0
      ? `<div style="display:flex;align-items:center;gap:6px;margin:6px 0 2px">
           <span style="background:#4a7c3022;color:#4a7c30;font-size:11px;font-weight:700;padding:2px 8px;border-radius:12px;border:1px solid #4a7c3044">${spot.pricePerHour} RON / h</span>
         </div>`
      : '';
    const fishHtml = spot.parsedFishSpecies.length > 0
      ? `<div style="color:#93c5fd;font-size:11px;margin-bottom:6px;line-height:1.35">Fish: ${spot.parsedFishSpecies.join(', ')}</div>`
      : '';

    const pontoonSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:middle;margin-right:5px;margin-bottom:1px"><rect x="2" y="7" width="20" height="10" rx="2"/><path d="M7 7V5a2 2 0 0 1 4 0v2M13 7V5a2 2 0 0 1 4 0v2"/><line x1="12" y1="12" x2="12" y2="12.01"/></svg>`;
    const navSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:middle;margin-right:5px;margin-bottom:1px"><polyline points="3 11 22 2 13 21 11 13 3 11"/></svg>`;

    let html = `<div style="min-width:195px;font-family:inherit">`;
    html += `<div style="font-size:15px;font-weight:700;color:#1e293b;margin-bottom:3px">${spot.name}</div>`;
    if (spot.description) {
      html += `<div style="color:#64748b;font-size:12px;margin-bottom:4px;line-height:1.4">${spot.description}</div>`;
    }
    html += priceHtml;
    html += fishHtml;
    html += `<div style="color:#94a3b8;font-size:10px;margin-bottom:10px">${spot.latitude.toFixed(5)}, ${spot.longitude.toFixed(5)}</div>`;
    html += `<button class="popup-book-btn" style="display:flex;align-items:center;justify-content:center;padding:8px 14px;border-radius:8px;font-size:12px;font-weight:600;cursor:pointer;width:100%;background:#4a7c30;color:#fff;border:none;transition:filter .15s;">${pontoonSvg}Book Pontoon</button>`;
    html += `<button class="popup-route-btn" style="display:flex;align-items:center;justify-content:center;padding:7px 14px;border-radius:8px;font-size:12px;font-weight:600;cursor:pointer;width:100%;margin-top:6px;background:#1e3a5f;color:#60a5fa;border:1px solid #2563eb55;transition:all .15s">${navSvg}Route on map</button>`;
    html += `</div>`;
    return html;
  }

  showRouteOnMap(spot: FishingSpot): void {
    const drawRoute = (origin: L.LatLng) => {
      const url =
        `https://router.project-osrm.org/route/v1/driving/` +
        `${origin.lng},${origin.lat};${spot.longitude},${spot.latitude}` +
        `?overview=full&geometries=geojson`;

      fetch(url)
        .then(r => r.json())
        .then(data => {
          if (data.code !== 'Ok' || !data.routes?.length) {
            this.showToast('Could not calculate route', 'error');
            return;
          }
          const route = data.routes[0];
          const distKm = (route.distance / 1000).toFixed(1);
          const durationMin = Math.round(route.duration / 60);
          const durationText = durationMin >= 60
            ? `${Math.floor(durationMin / 60)}h ${durationMin % 60}min`
            : `${durationMin} min`;

          // Remove existing route
          if (this.routeLayer) this.map.removeLayer(this.routeLayer);

          this.routeLayer = L.geoJSON(route.geometry, {
            style: {
              color: '#3b82f6',
              weight: 5,
              opacity: 0.85,
              lineJoin: 'round',
              lineCap: 'round'
            }
          }).addTo(this.map);

          this.routeInfo = {
            distance: `${distKm} km`,
            duration: durationText,
            spotName: spot.name
          };

          this.map.fitBounds(this.routeLayer.getBounds(), { padding: [50, 50] });
        })
        .catch(() => this.showToast('Error calculating route', 'error'));
    };

    if (this.userLatLng) {
      drawRoute(this.userLatLng);
    } else if (navigator.geolocation) {
      this.locatingUser = true;
      this.showToast('Getting location...', 'success');
      navigator.geolocation.getCurrentPosition(
        (pos) => this.zone.run(() => {
          this.locatingUser = false;
          const userLatLng = L.latLng(pos.coords.latitude, pos.coords.longitude);
          this.setUserLocation(userLatLng, pos.coords.accuracy, false);
          this.persistUserLocation(userLatLng, pos.coords.accuracy);
          drawRoute(userLatLng);
        }),
        () => this.zone.run(() => {
          this.locatingUser = false;
          this.showToast('Enable location to view the route on the map', 'error');
        }),
        { enableHighAccuracy: true, timeout: 8000 }
      );
    } else {
      this.showToast('Geolocation is not supported by the browser', 'error');
    }
  }

  clearRoute(): void {
    if (this.routeLayer) {
      this.map.removeLayer(this.routeLayer);
      this.routeLayer = null;
    }
    this.routeInfo = null;
  }

  locateUser(): void {
    if (!navigator.geolocation) {
      this.showToast('Geolocation is not supported by your browser', 'error');
      return;
    }
    this.locatingUser = true;
    navigator.geolocation.getCurrentPosition(
      (position) => this.zone.run(() => {
        this.locatingUser = false;
        const { latitude, longitude, accuracy } = position.coords;
        const latlng = L.latLng(latitude, longitude);
        this.setUserLocation(latlng, accuracy, true);
        this.persistUserLocation(latlng, accuracy);
        this.showToast('Location found!', 'success');
      }),
      (err) => this.zone.run(() => {
        this.locatingUser = false;
        const messages: Record<number, string> = {
          1: 'Location access was denied',
          2: 'Location cannot be determined',
          3: 'Location request timed out'
        };
        this.showToast(messages[err.code] ?? 'Error getting location', 'error');
      }),
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }

  toggleAddMode(): void {
    if (!this.canEdit) return;
    this.isAddMode = !this.isAddMode;
    this.isDeleteMode = false;

    if (this.isAddMode) {
      this.map.getContainer().style.cursor = 'crosshair';
      this.map.once('click', (e: L.LeafletMouseEvent) => this.onMapClick(e));
      // Lazy-load managers only when the add-spot form is actually opened
      if (this.managers.length === 0) {
        this.userService.getManagers().subscribe({
          next: (managers) => { this.managers = managers; },
          error: () => {}
        });
      }
    } else {
      this.map.getContainer().style.cursor = '';
      this.cancelAddSpot();
    }
  }

  private onMapClick(e: L.LeafletMouseEvent): void {
    if (!this.isAddMode) return;

    this.newSpotLat = e.latlng.lat;
    this.newSpotLng = e.latlng.lng;
    this.newSpotName = '';
    this.newSpotDescription = '';
    this.newSpotPrice = 0;
    this.showSpotForm = true;

    this.pendingMarker = L.marker([e.latlng.lat, e.latlng.lng], { opacity: 0.6 }).addTo(this.map);
    this.map.getContainer().style.cursor = '';
    this.isAddMode = false;
  }

  confirmAddSpot(): void {
    if (!this.newSpotName.trim()) {
      this.showToast('Please enter a spot name', 'error');
      return;
    }

    const userId = this.authService.getUserId();
    if (!userId) return;

    const spot: CreateFishingSpot = {
      name: this.newSpotName.trim(),
      description: this.newSpotDescription.trim() || undefined,
      latitude: this.newSpotLat,
      longitude: this.newSpotLng,
      pricePerHour: this.newSpotPrice,
      userId: userId,
      managerId: this.newSpotManagerId ?? undefined
    };

    this.fishingSpotService.create(spot).subscribe({
      next: () => {
        this.showToast('Fishing spot added!', 'success');
        this.adminService.clearStatsCache();
        this.cancelAddSpot();
        this.loadSpots();
      },
      error: () => this.showToast('Failed to create spot', 'error')
    });
  }

  cancelAddSpot(): void {
    this.showSpotForm = false;
    this.newSpotManagerId = null;
    if (this.pendingMarker) {
      this.map.removeLayer(this.pendingMarker);
      this.pendingMarker = null;
    }
    this.map.getContainer().style.cursor = '';
    this.isAddMode = false;
  }

  toggleDeleteMode(): void {
    if (!this.canEdit) return;
    this.isDeleteMode = !this.isDeleteMode;
    this.isAddMode = false;
    this.map.getContainer().style.cursor = this.isDeleteMode ? 'pointer' : '';
  }

  private deleteSpot(spot: FishingSpot, marker: L.Marker): void {
    if (!confirm(`Delete "${spot.name}"?`)) return;

    this.fishingSpotService.delete(spot.id).subscribe({
      next: () => {
        this.showToast(`"${spot.name}" deleted`, 'success');
        this.adminService.clearStatsCache();
        this.loadSpots();
      },
      error: () => this.showToast('Failed to delete spot', 'error')
    });
  }

  private showToast(message: string, type: 'success' | 'error'): void {
    this.showMessage = message;
    this.messageType = type;
    setTimeout(() => this.showMessage = '', 3000);
  }

  toggleMapExpanded(): void {
    this.mapExpanded = !this.mapExpanded;
    // Give the DOM time to update, then invalidate map size
    setTimeout(() => {
      this.map.invalidateSize();
    }, 350);
  }

  onFishSpeciesFilterChange(species: string): void {
    this.selectedFishSpecies = species;
    this.applyFishFilter();
  }

  @HostListener('window:resize')
  onViewportResize(): void {
    if (!this.map) return;
    requestAnimationFrame(() => this.map.invalidateSize());
  }

  setSessionView(view: 'future' | 'past'): void {
    if (this.selectedSessionView === view) return;

    this.selectedSessionView = view;
    this.updateSelectedSession();
  }

  getSessionEmptyMessage(): string {
    return this.selectedSessionView === 'future'
      ? 'No future purchased sessions found.'
      : 'No past purchased sessions found.';
  }

  async toggleSessionQrCode(): Promise<void> {
    const booking = this.latestPurchasedSession;
    if (!booking) return;

    if (this.isSessionQrVisible) {
      this.isSessionQrVisible = false;
      return;
    }

    if (this.sessionQrCode && this.sessionQrBookingId === booking.id) {
      this.isSessionQrVisible = true;
      return;
    }

    this.isSessionQrLoading = true;
    try {
      const content = [
        `WhereWeFishin - Booking #${booking.id}`,
        `Username: ${this.authService.getUsername()}`,
        `Booking ID: #${booking.id}`,
        `Spot: ${booking.fishingSpotName}`,
        `Start: ${new Date(booking.startDate).toLocaleString('en-US')}`,
        `Duration: ${booking.durationHours}h`,
        `Total: ${booking.totalPrice.toFixed(2)} RON`,
        `Status: ${booking.status}`
      ].join('\n');

      const { default: QRCode } = await import('qrcode');
      this.sessionQrCode = await QRCode.toDataURL(content, { width: 180, margin: 1 });
      this.sessionQrBookingId = booking.id;
      this.isSessionQrVisible = true;
    } catch {
      this.showToast('Failed to generate QR code', 'error');
    } finally {
      this.isSessionQrLoading = false;
    }
  }

  formatBookingDate(dateValue: string): string {
    const parsed = new Date(dateValue);
    if (Number.isNaN(parsed.getTime())) {
      return '-';
    }

    return new Intl.DateTimeFormat('en-US', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(parsed);
  }

  getBookingStatusClass(status: string): string {
    const normalized = status.toLowerCase();
    if (normalized === 'cancelled') return 'booking-status-cancelled';
    if (normalized === 'confirmed') return 'booking-status-confirmed';
    if (normalized === 'completed') return 'booking-status-completed';
    return 'booking-status-pending';
  }

  isAdmin(): boolean {
    return this.currentRole === 'Admin';
  }

  isManager(): boolean {
    return this.currentRole === 'Manager';
  }

  isUser(): boolean {
    return this.currentRole === 'User';
  }

  openSpotDetails(spotId: number): void {
    this.router.navigate(['/spots', spotId]);
  }

  private updateSelectedSession(): void {
    this.latestPurchasedSession = this.selectedSessionView === 'future'
      ? this.latestFutureSession
      : this.latestPastSession;

    this.isSessionQrVisible = false;
    this.isSessionQrLoading = false;
    this.sessionQrCode = '';
    this.sessionQrBookingId = null;
  }

  private enrichSpotWithSpecies(spot: FishingSpot): HomeSpot {
    // Use managed fish species from the spot if available
    if (spot.fishSpecies) {
      try {
        const managed: string[] = JSON.parse(spot.fishSpecies);
        if (managed.length > 0) {
          return { ...spot, parsedFishSpecies: managed };
        }
      } catch {}
    }

    const normalizedName = spot.name.trim().toLowerCase();
    const mappedSpecies = this.spotSpeciesByName[normalizedName];
    const fallbackSpecies = this.fallbackSpeciesSets[spot.id % this.fallbackSpeciesSets.length];

    return {
      ...spot,
      parsedFishSpecies: [...(mappedSpecies ?? fallbackSpecies)]
    };
  }

  private applyFishFilter(): void {
    this.visibleSpots = this.selectedFishSpecies === 'all'
      ? [...this.spots]
      : this.spots.filter(spot => spot.parsedFishSpecies.includes(this.selectedFishSpecies));

    this.rebuildClosestSpots();
    this.renderMarkers();
  }

  private setUserLocation(latlng: L.LatLng, accuracy: number, animate: boolean): void {
    this.userLatLng = latlng;
    this.hasUserLocation = true;

    if (this.userLocationMarker) this.map.removeLayer(this.userLocationMarker);
    if (this.userLocationCircle) this.map.removeLayer(this.userLocationCircle);

    const accuracyText = accuracy < 1000
      ? `±${Math.round(accuracy)} m`
      : `±${(accuracy / 1000).toFixed(1)} km`;

    // Cerc de acuratețe — se redimensionează automat la zoom
    this.userLocationCircle = L.circle(latlng, {
      radius: accuracy,
      color: '#4285f4',
      weight: 1.5,
      opacity: 0.4,
      fillColor: '#4285f4',
      fillOpacity: 0.1,
      interactive: false
    }).addTo(this.map);

    // Punct animat cu DivIcon — stiluri inline pentru a evita probleme CSS
    const icon = L.divIcon({
      className: '',
      html: `
        <div style="position:relative;width:18px;height:18px;overflow:visible">
          <div style="
            position:absolute;width:44px;height:44px;
            background:rgba(66,133,244,0.22);border-radius:50%;
            top:50%;left:50%;
            transform:translate(-50%,-50%) scale(0.3);
            animation:userLocPulse 2.2s ease-out infinite;
            pointer-events:none;
          "></div>
          <div style="
            position:absolute;width:18px;height:18px;
            background:#4285f4;border:3px solid #fff;border-radius:50%;
            box-shadow:0 2px 10px rgba(66,133,244,0.6);
            top:0;left:0;cursor:pointer;
            transition:transform 0.15s ease;
          "></div>
        </div>`,
      iconSize: [18, 18],
      iconAnchor: [9, 9],
      popupAnchor: [0, -14]
    });

    this.userLocationMarker = L.marker(latlng, { icon, zIndexOffset: 1000 })
      .bindPopup(
        `<div style="font-family:inherit;min-width:160px">
          <b style="font-size:13px;color:#1e293b">Locația ta</b><br>
          <span style="font-size:11px;color:#64748b">Acuratețe GPS: <b>${accuracyText}</b></span><br>
          <span style="font-size:11px;color:#94a3b8">Se caută adresa...</span>
        </div>`,
        { maxWidth: 240 }
      )
      .addTo(this.map);

    this.userLocationMarker.on('click', () => {
      this.userLocationMarker!.openPopup();
      fetch(`https://nominatim.openstreetmap.org/reverse?lat=${latlng.lat}&lon=${latlng.lng}&format=json&accept-language=ro`)
        .then(r => r.json())
        .then((data: { display_name?: string; address?: Record<string, string> }) => {
          const a = data.address ?? {};
          const place = [a['road'], a['suburb'], a['city'] ?? a['town'] ?? a['village'], a['county']]
            .filter(Boolean).join(', ') || data.display_name || `${latlng.lat.toFixed(5)}, ${latlng.lng.toFixed(5)}`;
          this.userLocationMarker!.setPopupContent(
            `<div style="font-family:inherit;min-width:160px">
              <b style="font-size:13px;color:#1e293b">Locația ta</b><br>
              <span style="font-size:12px;color:#334155">${place}</span><br>
              <span style="font-size:11px;color:#64748b;margin-top:3px;display:block">Acuratețe GPS: <b>${accuracyText}</b></span>
            </div>`
          );
        })
        .catch(() => {
          this.userLocationMarker!.setPopupContent(
            `<div style="font-family:inherit">
              <b style="font-size:13px;color:#1e293b">Locația ta</b><br>
              <span style="font-size:11px;color:#64748b">Acuratețe GPS: <b>${accuracyText}</b></span>
            </div>`
          );
        });
    });

    this.rebuildClosestSpots();
    this.cdr.detectChanges();

    if (animate) {
      this.map.flyTo(latlng, 14, { duration: 1.2 });
    }
  }

  private rebuildClosestSpots(): void {
    const origin = this.userLatLng;
    if (!origin || this.visibleSpots.length === 0) {
      this.closestSpots = [];
      return;
    }

    this.closestSpots = this.visibleSpots
      .filter(spot => Number.isFinite(spot.latitude) && Number.isFinite(spot.longitude))
      .map(spot => {
        const distanceMeters = origin.distanceTo(L.latLng(spot.latitude, spot.longitude));
        return {
          spot,
          distanceMeters,
          distanceLabel: this.formatDistance(distanceMeters)
        };
      })
      .sort((a, b) => a.distanceMeters - b.distanceMeters)
      .slice(0, this.closestSpotsLimit);
  }

  private formatDistance(distanceMeters: number): string {
    if (distanceMeters < 1000) {
      return `${Math.round(distanceMeters)} m`;
    }

    const distanceKm = distanceMeters / 1000;
    if (distanceKm < 10) {
      return `${distanceKm.toFixed(1)} km`;
    }

    return `${Math.round(distanceKm)} km`;
  }

  private persistUserLocation(latlng: L.LatLng, accuracy: number): void {
    try {
      localStorage.setItem(this.userLocationStorageKey, JSON.stringify({
        latitude: latlng.lat,
        longitude: latlng.lng,
        accuracy,
        savedAt: Date.now()
      }));
    } catch {
      // Ignore storage errors (private browsing / storage disabled).
    }
  }

  private restoreCachedUserLocation(): boolean {
    try {
      const raw = localStorage.getItem(this.userLocationStorageKey);
      if (!raw) return false;

      const cached = JSON.parse(raw) as {
        latitude: number;
        longitude: number;
        accuracy?: number;
        savedAt?: number;
      };

      const isValid = Number.isFinite(cached.latitude) && Number.isFinite(cached.longitude);
      if (!isValid) return false;

      const savedAt = cached.savedAt ?? 0;
      if (Date.now() - savedAt > this.userLocationMaxAgeMs) {
        return false;
      }

      const cachedLatLng = L.latLng(cached.latitude, cached.longitude);
      this.setUserLocation(cachedLatLng, cached.accuracy ?? 35, false);
      this.map.setView(cachedLatLng, 13);
      return true;
    } catch {
      return false;
    }
  }
}
