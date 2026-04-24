import { Component, OnInit, AfterViewInit, OnDestroy, HostListener, ChangeDetectorRef, NgZone, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { FishingSpotService } from '../../services/fishing-spot.service';
import { FishingSpot, CreateFishingSpot } from '../../models/fishing-spot.model';
import { AdminService, AdminStats } from '../../services/admin.service';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { BookingService } from '../../services/booking.service';
import { Booking } from '../../models/booking.model';
import { UserService } from '../../services/user.service';
import { User } from '../../models/user.model';
import { RoutingService } from '../../services/routing.service';
import { GeocodingService } from '../../services/geocoding.service';
import { AppIcon } from '../../shared/icons/app-icon';
import { AppIconName } from '../../shared/icons/app-icon.registry';
import {
  buildFallbackUserLocationPopup,
  buildHomeSpotPopupContent,
  buildPendingUserLocationPopup,
  buildResolvedUserLocationPopup,
  createHomeSpotIcon,
  createHomeUserLocationIcon
} from './home-map.helpers';
import * as L from 'leaflet';

interface NearbySpot {
  spot: HomeSpot;
  distanceMeters: number;
  distanceLabel: string;
}

interface HomeSpot extends FishingSpot {
  parsedFishSpecies: string[];
}

type HomeStatIcon = Extract<AppIconName, 'bookings' | 'success' | 'error' | 'users' | 'deactivated' | 'spots' | 'pontoons' | 'analyses' | 'visited'>;

interface HomeStatCard {
  key: string;
  value: number;
  label: string;
  icon: HomeStatIcon;
  iconClass: string;
}

@Component({
  selector: 'app-home',
  imports: [CommonModule, FormsModule, AppIcon],
  templateUrl: './home.html',
  styleUrl: './home.css',
  encapsulation: ViewEncapsulation.None
})
export class Home implements OnInit, AfterViewInit, OnDestroy {
  private map!: L.Map;
  private markersLayer!: L.LayerGroup;
  private readonly mapClickHandler = (event: L.LeafletMouseEvent) => this.onMapClick(event);
  private readonly spotIcon = createHomeSpotIcon();
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
  readonly closestSpotsLimit = 4;
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
  userBookingsCount = 0;
  userVisitedLakesCount = 0;
  loadingStats = true;
  loadingLatestSession = true;
  statCards: HomeStatCard[] = [];
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
    private routingService: RoutingService,
    private geocodingService: GeocodingService,
    private cdr: ChangeDetectorRef,
    private zone: NgZone
  ) {}

  ngOnInit(): void {
    this.canEdit = this.authService.isManagerOrAdmin();
    this.currentRole = this.authService.getRole() || 'User';
    this.refreshStatCards();
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
      this.refreshStatCards();
      return;
    }

    forkJoin({
      spots:    this.fishingSpotService.getAll(),
      analyses: this.videoAnalysisService.getUserAnalyses(userId).pipe(
        catchError(() => of([]))
      ),
      bookings: this.isUser()
        ? this.bookingService.getMyBookings().pipe(catchError(() => of([] as Booking[])))
        : of([] as Booking[]),
      stats: this.isAdmin()
        ? this.adminService.getStats().pipe(catchError(() => of(null)))
        : of(null)
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
        if (this.isUser()) {
          const active = bookings.filter(b => b.status?.toLowerCase() !== 'cancelled');
          this.userBookingsCount = active.length;
          this.userVisitedLakesCount = new Set(active.map(b => b.fishingSpotId)).size;
          const now = Date.now();
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
        this.refreshStatCards();
      },
      error: () => {
        this.showToast('Failed to load fishing spots', 'error');
        this.loadingStats = false;
        this.loadingLatestSession = false;
        this.refreshStatCards();
      }
    });
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.off('click', this.mapClickHandler);
      this.map.remove();
    }
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
    this.map.on('click', this.mapClickHandler);
  }


  private renderMarkers(): void {
    if (!this.markersLayer) return;  // map not yet initialized (cache sync emit in ngOnInit)
    this.markersLayer.clearLayers();

    this.visibleSpots.forEach(spot => {
      const marker = L.marker([spot.latitude, spot.longitude], {
        icon: this.spotIcon
      }).bindPopup(buildHomeSpotPopupContent(spot), {
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

      this.markersLayer.addLayer(marker);
    });
  }

  showRouteOnMap(spot: FishingSpot): void {
    const drawRoute = (origin: L.LatLng) => {
      this.routingService.getRoute(origin.lng, origin.lat, spot.longitude, spot.latitude).subscribe({
        next: (result) => {
          // Remove existing route
          if (this.routeLayer) this.map.removeLayer(this.routeLayer);

          this.routeLayer = L.geoJSON(result.geometry as any, {
            style: {
              color: '#3b82f6',
              weight: 5,
              opacity: 0.85,
              lineJoin: 'round',
              lineCap: 'round'
            }
          }).addTo(this.map);

          this.routeInfo = {
            distance: `${result.distanceKm} km`,
            duration: result.durationText,
            spotName: spot.name
          };

          this.map.fitBounds(this.routeLayer.getBounds(), { padding: [50, 50] });
        },
        error: () => this.showToast('Error calculating route', 'error')
      });
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
      managerId: this.newSpotManagerId ?? undefined
    };

    this.fishingSpotService.create(spot).subscribe({
      next: () => {
        this.showToast('Fishing spot added!', 'success');
        this.adminService.clearStatsCache();
        this.cancelAddSpot();
        this.loadDashboardData();
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
        this.loadDashboardData();
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

  private refreshStatCards(): void {
    if (this.isAdmin()) {
      this.statCards = [
        { key: 'total-bookings', value: this.stats?.totalBookings ?? 0, label: 'Total Bookings', icon: 'bookings', iconClass: 'bookings-icon' },
        { key: 'confirmed-bookings', value: this.stats?.confirmedBookings ?? 0, label: 'Confirmed', icon: 'success', iconClass: 'success-icon' },
        { key: 'cancelled-bookings', value: this.stats?.cancelledBookings ?? 0, label: 'Cancelled', icon: 'error', iconClass: 'error-icon' },
        { key: 'total-users', value: this.stats?.totalUsers ?? 0, label: 'Active Users', icon: 'users', iconClass: 'users-icon' },
        { key: 'deactivated-users', value: this.stats?.deactivatedUsers ?? 0, label: 'Deactivated', icon: 'deactivated', iconClass: 'deactivated-icon' },
        { key: 'total-spots', value: this.stats?.totalSpots ?? 0, label: 'Fishing Spots', icon: 'spots', iconClass: 'spots-icon' },
        { key: 'total-pontoons', value: this.stats?.totalPontoons ?? 0, label: 'Pontoons', icon: 'pontoons', iconClass: 'pontoons-icon' },
        { key: 'total-analyses', value: this.stats?.totalAnalyses ?? 0, label: 'AI Analyses', icon: 'analyses', iconClass: 'analyses-icon' }
      ];
      return;
    }

    const personalCards: HomeStatCard[] = [
      { key: 'my-analyses', value: this.userAnalysesCount, label: 'My Analyses', icon: 'analyses', iconClass: 'analyses-icon' },
      { key: 'completed-analyses', value: this.userCompletedCount, label: 'Completed', icon: 'success', iconClass: 'success-icon' }
    ];

    if (this.isUser()) {
      personalCards.push(
        { key: 'my-bookings', value: this.userBookingsCount, label: 'My Bookings', icon: 'bookings', iconClass: 'bookings-icon' },
        { key: 'visited-lakes', value: this.userVisitedLakesCount, label: 'Visited Lakes', icon: 'visited', iconClass: 'visited-icon' }
      );
    }

    if (this.isManager()) {
      personalCards.push(
        { key: 'my-spots', value: this.userSpotsCount, label: 'My Spots', icon: 'spots', iconClass: 'spots-icon' }
      );
    }

    this.statCards = personalCards;
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
    const icon = createHomeUserLocationIcon();

    this.userLocationMarker = L.marker(latlng, { icon, zIndexOffset: 1000 })
      .bindPopup(buildPendingUserLocationPopup(accuracyText), { maxWidth: 240 })
      .addTo(this.map);

    this.userLocationMarker.on('click', () => {
      this.userLocationMarker!.openPopup();
      this.geocodingService.reverseGeocode(latlng.lat, latlng.lng).subscribe({
        next: (result) => {
          const a = result.address ?? {};
          const place = [a['road'], a['suburb'], a['city'] ?? a['town'] ?? a['village'], a['county']]
            .filter(Boolean).join(', ') || result.displayName;
          this.userLocationMarker!.setPopupContent(buildResolvedUserLocationPopup(place, accuracyText));
        },
        error: () => {
          this.userLocationMarker!.setPopupContent(buildFallbackUserLocationPopup(accuracyText));
        }
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
