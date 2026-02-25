import { Component, OnInit, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { FishingSpotService, FishingSpot, CreateFishingSpot } from '../../services/fishing-spot.service';
import { AdminService, AdminStats } from '../../services/admin.service';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { VideoAnalysis } from '../../models/video-analysis.model';
import { CartService } from '../../services/cart.service';
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
  locatingUser = false;

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
    public cartService: CartService,
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
    // Auto-locate user on page load
    setTimeout(() => this.locateUser(), 500);
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

  private createSpotIcon(inCart: boolean): L.DivIcon {
    const fillColor = inCart ? '#166534' : '#4a7c30';
    const ringColor = inCart ? '#4ade80' : '#c8e6c0';
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
        icon: this.createSpotIcon(this.cartService.isInCart(spot.id))
      }).bindPopup(this.buildPopupContent(spot), { maxWidth: 220 });

      marker.on('click', () => {
        if (this.isDeleteMode && this.canEdit) {
          this.deleteSpot(spot, marker);
        }
      });

      marker.on('popupopen', (e: any) => {
        const container: HTMLElement | undefined = e.popup.getElement();
        const btn = container?.querySelector<HTMLButtonElement>('.popup-cart-btn');
        if (btn) {
          btn.onclick = () => this.addToCart(spot);
        }
        const viewBtn = container?.querySelector<HTMLButtonElement>('.popup-view-btn');
        if (viewBtn) {
          viewBtn.onclick = () => this.router.navigate(['/spots', spot.id]);
        }
      });

      this.markerSpotMap.set(marker, spot);
      this.markersLayer.addLayer(marker);
    });
  }

  private buildPopupContent(spot: FishingSpot): string {
    const inCart = this.cartService.isInCart(spot.id);

    const cartIconSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:middle;margin-right:5px;margin-bottom:1px"><circle cx="9" cy="21" r="1"/><circle cx="20" cy="21" r="1"/><path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"/></svg>`;
    const checkIconSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:middle;margin-right:5px;margin-bottom:1px"><polyline points="20 6 9 17 4 12"/></svg>`;

    const btnIcon = inCart ? checkIconSvg : cartIconSvg;
    const btnLabel = inCart ? 'In Cart — Go to Cart' : 'Add to Cart';
    const btnStyle = inCart
      ? 'background:#14532d;color:#4ade80;border:1px solid #166534;'
      : 'background:#4a7c30;color:#fff;border:none;';

    const priceHtml = spot.pricePerHour > 0
      ? `<div style="display:flex;align-items:center;gap:6px;margin:6px 0 2px">
           <span style="background:#4a7c3022;color:#4a7c30;font-size:11px;font-weight:700;padding:2px 8px;border-radius:12px;border:1px solid #4a7c3044">${spot.pricePerHour} RON / h</span>
         </div>`
      : '';

    let html = `<div style="min-width:195px;font-family:inherit">`;
    html += `<div style="font-size:15px;font-weight:700;color:#1e293b;margin-bottom:3px">${spot.name}</div>`;
    if (spot.description) {
      html += `<div style="color:#64748b;font-size:12px;margin-bottom:4px;line-height:1.4">${spot.description}</div>`;
    }
    html += priceHtml;
    html += `<div style="color:#94a3b8;font-size:10px;margin-bottom:10px">${spot.latitude.toFixed(5)}, ${spot.longitude.toFixed(5)}</div>`;
    html += `<button class="popup-cart-btn" style="display:flex;align-items:center;justify-content:center;padding:8px 14px;border-radius:8px;font-size:12px;font-weight:600;cursor:pointer;width:100%;transition:filter .15s;${btnStyle}">${btnIcon}${btnLabel}</button>`;
    const eyeSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:middle;margin-right:5px;margin-bottom:1px"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>`;
    html += `<button class="popup-view-btn" style="display:flex;align-items:center;justify-content:center;padding:7px 14px;border-radius:8px;font-size:12px;font-weight:600;cursor:pointer;width:100%;margin-top:6px;background:transparent;color:#94a3b8;border:1px solid #334155;transition:all .15s">${eyeSvg}View Spot</button>`;
    html += `</div>`;
    return html;
  }

  addToCart(spot: FishingSpot): void {
    if (this.cartService.isInCart(spot.id)) {
      this.router.navigate(['/cart']);
      return;
    }
    const defaultStart = new Date();
    defaultStart.setHours(defaultStart.getHours() + 1, 0, 0, 0);
    defaultStart.setMinutes(defaultStart.getMinutes() - defaultStart.getTimezoneOffset());
    this.cartService.addItem({
      spotId: spot.id,
      spotName: spot.name,
      latitude: spot.latitude,
      longitude: spot.longitude,
      pricePerHour: spot.pricePerHour,
      durationHours: 24,
      startDate: defaultStart.toISOString().slice(0, 16)
    });
    this.showToast(`"${spot.name}" added to cart`, 'success');
    // Re-render markers so button state updates
    this.renderMarkers();
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

        // Remove previous location layers
        if (this.userLocationMarker) this.map.removeLayer(this.userLocationMarker);
        if (this.userLocationCircle) this.map.removeLayer(this.userLocationCircle);

        // Accuracy circle
        this.userLocationCircle = L.circle(latlng, {
          radius: accuracy,
          color: '#3b82f6',
          fillColor: '#3b82f6',
          fillOpacity: 0.1,
          weight: 1
        }).addTo(this.map);

        // Pulsing dot icon
        const icon = L.divIcon({
          className: '',
          html: `<div class="user-location-dot"><div class="user-location-pulse"></div></div>`,
          iconSize: [20, 20],
          iconAnchor: [10, 10]
        });

        this.userLocationMarker = L.marker(latlng, { icon })
          .bindPopup(`<b>Locația ta</b><br>Precizie: ~${Math.round(accuracy)} m`)
          .addTo(this.map);

        this.map.flyTo(latlng, 14, { duration: 1.5 });
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
}
