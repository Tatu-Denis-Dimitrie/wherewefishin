import { Component, OnInit, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { FishingSpotService, FishingSpot, CreateFishingSpot } from '../../services/fishing-spot.service';
import { AdminService, AdminStats } from '../../services/admin.service';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { VideoAnalysis } from '../../models/video-analysis.model';
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
  private pendingMarker: L.Marker | null = null;

  constructor(
    private authService: AuthService,
    private fishingSpotService: FishingSpotService,
    private adminService: AdminService,
    private videoAnalysisService: VideoAnalysisService,
    private router: Router
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

  private initMap(): void {
    const iconDefault = L.icon({
      iconRetinaUrl: 'assets/marker-icon-2x.png',
      iconUrl: 'assets/marker-icon.png',
      shadowUrl: 'assets/marker-shadow.png',
      iconSize: [25, 41],
      iconAnchor: [12, 41],
      popupAnchor: [1, -34],
      tooltipAnchor: [16, -28],
      shadowSize: [41, 41]
    });
    L.Marker.prototype.options.icon = iconDefault;

    this.map = L.map('map', {
      center: [45.9432, 24.9668],
      zoom: 8,
      maxZoom: 18
    });

    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
      attribution: 'Tiles &copy; Esri',
      maxZoom: 18
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
      const marker = L.marker([spot.latitude, spot.longitude])
        .bindPopup(this.buildPopupContent(spot));

      marker.on('click', () => {
        if (this.isDeleteMode && this.canEdit) {
          this.deleteSpot(spot, marker);
        }
      });

      this.markerSpotMap.set(marker, spot);
      this.markersLayer.addLayer(marker);
    });
  }

  private buildPopupContent(spot: FishingSpot): string {
    let html = `<div style="min-width:160px">
      <strong style="font-size:15px">${spot.name}</strong>`;
    if (spot.description) {
      html += `<br><span style="color:#64748b;font-size:13px">${spot.description}</span>`;
    }
    html += `<br><span style="color:#94a3b8;font-size:11px">${spot.latitude.toFixed(5)}, ${spot.longitude.toFixed(5)}</span>`;
    html += `</div>`;
    return html;
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
      userId: userId
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
