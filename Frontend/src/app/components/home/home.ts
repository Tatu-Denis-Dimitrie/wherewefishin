import { Component, OnInit, AfterViewInit, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { FishingSpotService, FishingSpot, CreateFishingSpot } from '../../services/fishing-spot.service';
import { AdminService, AdminStats } from '../../services/admin.service';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { VideoAnalysis } from '../../models/video-analysis.model';
import { UserService } from '../../services/user.service';
import { User } from '../../models/user.model';
import * as L from 'leaflet';

@Component({
  selector: 'app-home',
  imports: [CommonModule, FormsModule],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class Home implements OnInit, AfterViewInit, OnDestroy {
  private map!: L.Map;
  private markersLayer!: L.LayerGroup;
  private markerSpotMap = new Map<L.Marker, FishingSpot>();
  private userLocationMarker: L.Marker | null = null;
  private userLocationCircle: L.Circle | null = null;
  private userLatLng: L.LatLng | null = null;
  private routeLayer: L.GeoJSON | null = null;
  private readonly userLocationStorageKey = 'wherewefishin.user-location.v1';
  private readonly userLocationMaxAgeMs = 1000 * 60 * 60 * 24 * 7;
  locatingUser = false;
  routeInfo: { distance: string; duration: string; spotName: string } | null = null;

  spots: FishingSpot[] = [];
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
  recentAnalyses: VideoAnalysis[] = [];
  loadingStats = true;
  
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
    private router: Router,
    private userService: UserService
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
    this.loadSpots();
    const restoredFromCache = this.restoreCachedUserLocation();
    if (!restoredFromCache) {
      // Ask for geolocation only when we don't have a cached location.
      setTimeout(() => this.locateUser(), 500);
    }
    setTimeout(() => this.map.invalidateSize(), 250);
  }

  private loadDashboardData(): void {
    this.loadingStats = true;
    const userId = this.authService.getUserId();
    
    if (!userId) {
      this.loadingStats = false;
      return;
    }

    // Load user's analyses (for all roles)
    this.videoAnalysisService.getUserAnalyses(userId).subscribe({
      next: (analyses) => {
        this.userAnalysesCount = analyses.length;
        this.userCompletedCount = analyses.filter(a => a.status === 'Completed').length;
        this.recentAnalyses = analyses.slice(0, 3);
        this.loadingStats = false;
      },
      error: () => {
        this.loadingStats = false;
      }
    });

    // Load manager/admin specific data
    if (this.isManager() || this.isAdmin()) {
      this.fishingSpotService.getAll().subscribe({
        next: (allSpots) => {
          this.userSpotsCount = allSpots.filter(s => s.userId === userId).length;
        },
        error: () => {}
      });

      this.userService.getManagers().subscribe({
        next: (managers) => { this.managers = managers; },
        error: () => {}
      });
    }
    
    // Load admin system stats
    if (this.isAdmin()) {
      this.adminService.getStats().subscribe({
        next: (stats) => {
          this.stats = stats;
        },
        error: () => {}
      });
    }
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
    this.map = L.map('map', {
      center: [45.9432, 24.9668],
      zoom: 8,
      maxZoom: 20
    });

    L.tileLayer('https://mt1.google.com/vt/lyrs=y&hl=ro&x={x}&y={y}&z={z}', {
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
        this.spots = spots;
        this.renderMarkers();
      },
      error: () => this.showToast('Failed to load fishing spots', 'error')
    });
  }

  private renderMarkers(): void {
    this.markersLayer.clearLayers();
    this.markerSpotMap.clear();

    this.spots.forEach(spot => {
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

  private buildPopupContent(spot: FishingSpot): string {
    const priceHtml = spot.pricePerHour > 0
      ? `<div style="display:flex;align-items:center;gap:6px;margin:6px 0 2px">
           <span style="background:#4a7c3022;color:#4a7c30;font-size:11px;font-weight:700;padding:2px 8px;border-radius:12px;border:1px solid #4a7c3044">${spot.pricePerHour} RON / h</span>
         </div>`
      : '';

    const pontoonSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:middle;margin-right:5px;margin-bottom:1px"><rect x="2" y="7" width="20" height="10" rx="2"/><path d="M7 7V5a2 2 0 0 1 4 0v2M13 7V5a2 2 0 0 1 4 0v2"/><line x1="12" y1="12" x2="12" y2="12.01"/></svg>`;
    const navSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:middle;margin-right:5px;margin-bottom:1px"><polyline points="3 11 22 2 13 21 11 13 3 11"/></svg>`;

    let html = `<div style="min-width:195px;font-family:inherit">`;
    html += `<div style="font-size:15px;font-weight:700;color:#1e293b;margin-bottom:3px">${spot.name}</div>`;
    if (spot.description) {
      html += `<div style="color:#64748b;font-size:12px;margin-bottom:4px;line-height:1.4">${spot.description}</div>`;
    }
    html += priceHtml;
    html += `<div style="color:#94a3b8;font-size:10px;margin-bottom:10px">${spot.latitude.toFixed(5)}, ${spot.longitude.toFixed(5)}</div>`;
    html += `<button class="popup-book-btn" style="display:flex;align-items:center;justify-content:center;padding:8px 14px;border-radius:8px;font-size:12px;font-weight:600;cursor:pointer;width:100%;background:#4a7c30;color:#fff;border:none;transition:filter .15s;">${pontoonSvg}Rezervă Ponton</button>`;
    html += `<button class="popup-route-btn" style="display:flex;align-items:center;justify-content:center;padding:7px 14px;border-radius:8px;font-size:12px;font-weight:600;cursor:pointer;width:100%;margin-top:6px;background:#1e3a5f;color:#60a5fa;border:1px solid #2563eb55;transition:all .15s">${navSvg}Traseu pe hartă</button>`;
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
            this.showToast('Nu s-a putut calcula ruta', 'error');
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
        .catch(() => this.showToast('Eroare la calcularea rutei', 'error'));
    };

    if (this.userLatLng) {
      drawRoute(this.userLatLng);
    } else if (navigator.geolocation) {
      this.locatingUser = true;
      this.showToast('Se obține locația...', 'success');
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          this.locatingUser = false;
          const userLatLng = L.latLng(pos.coords.latitude, pos.coords.longitude);
          this.setUserLocation(userLatLng, pos.coords.accuracy, false);
          this.persistUserLocation(userLatLng, pos.coords.accuracy);
          drawRoute(userLatLng);
        },
        () => {
          this.locatingUser = false;
          this.showToast('Activează locația pentru a vedea ruta pe hartă', 'error');
        },
        { enableHighAccuracy: true, timeout: 8000 }
      );
    } else {
      this.showToast('Geolocation nu este suportat de browser', 'error');
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
      (position) => {
        this.locatingUser = false;
        const { latitude, longitude, accuracy } = position.coords;
        const latlng = L.latLng(latitude, longitude);
        this.setUserLocation(latlng, accuracy, true);
        this.persistUserLocation(latlng, accuracy);
        this.showToast('Locație găsită!', 'success');
      },
      (err) => {
        this.locatingUser = false;
        const messages: Record<number, string> = {
          1: 'Accesul la locație a fost refuzat',
          2: 'Locația nu poate fi determinată',
          3: 'Cererea de locație a expirat'
        };
        this.showToast(messages[err.code] ?? 'Eroare la obținerea locației', 'error');
      },
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

  @HostListener('window:resize')
  onViewportResize(): void {
    if (!this.map) return;
    requestAnimationFrame(() => this.map.invalidateSize());
  }

  getAnalysisDisplayName(analysis: VideoAnalysis): string {
    const rawName = (analysis.fileName || '').trim();
    const fallbackName = `Upload ${this.getAnalysisDateLabel(analysis)}`;

    if (!rawName) {
      return fallbackName;
    }

    const nameWithoutExt = rawName.replace(/\.[A-Za-z0-9]{2,5}$/i, '');
    const cleanedName = nameWithoutExt
      .replace(/[_-]+/g, ' ')
      .replace(/\b\d{4,}\b/g, ' ')
      .replace(/\b(mp4|mov|avi|mkv|webm|video|recording|upload|clip|file|capture)\b/gi, ' ')
      .replace(/[^A-Za-z0-9ĂÂÎȘȚăâîșț\s]/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();

    if (!cleanedName || cleanedName.length < 3 || !/[A-Za-zĂÂÎȘȚăâîșț]/.test(cleanedName)) {
      return fallbackName;
    }

    const prettified = cleanedName
      .split(' ')
      .slice(0, 6)
      .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(' ');

    return prettified || fallbackName;
  }

  getAnalysisSummary(analysis: VideoAnalysis): string {
    const status = analysis.status?.toLowerCase() || '';
    if (status === 'completed') {
      const uniqueCount = analysis.totalUniqueFish ?? analysis.totalDetections;
      if (uniqueCount > 0) {
        return `${uniqueCount} capturi unice`;
      }
      return 'Analiză finalizată';
    }

    if (status === 'processing') {
      return 'Analiză în procesare';
    }

    if (status === 'failed') {
      return 'Procesare eșuată';
    }

    return 'Analiză nouă';
  }

  private getAnalysisDateLabel(analysis: VideoAnalysis): string {
    const sourceDate = analysis.createdAt || analysis.analyzedAt;
    const parsedDate = sourceDate ? new Date(sourceDate) : new Date();

    if (Number.isNaN(parsedDate.getTime())) {
      return `#${analysis.id}`;
    }

    return new Intl.DateTimeFormat('ro-RO', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    }).format(parsedDate);
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'completed': return 'status-completed';
      case 'processing': return 'status-processing';
      case 'failed': return 'status-failed';
      default: return 'status-pending';
    }
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

  private setUserLocation(latlng: L.LatLng, accuracy: number, animate: boolean): void {
    this.userLatLng = latlng;

    if (this.userLocationMarker) this.map.removeLayer(this.userLocationMarker);
    if (this.userLocationCircle) this.map.removeLayer(this.userLocationCircle);

    this.userLocationCircle = L.circle(latlng, {
      radius: accuracy,
      color: '#3b82f6',
      fillColor: '#3b82f6',
      fillOpacity: 0.1,
      weight: 1
    }).addTo(this.map);

    const icon = L.divIcon({
      className: '',
      html: `<div class="user-location-dot"><div class="user-location-pulse"></div></div>`,
      iconSize: [20, 20],
      iconAnchor: [10, 10]
    });

    this.userLocationMarker = L.marker(latlng, { icon })
      .bindPopup(`<b>Locația ta</b><br>Precizie: ~${Math.round(accuracy)} m`)
      .addTo(this.map);

    if (animate) {
      this.map.flyTo(latlng, 14, { duration: 1.2 });
    }
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
