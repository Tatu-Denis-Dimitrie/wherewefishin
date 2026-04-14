import { Component, HostListener, OnDestroy, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { FishingSpotService } from '../../services/fishing-spot.service';
import { FishingSpot } from '../../models/fishing-spot.model';
import { PontoonService, Pontoon, CreatePontoon } from '../../services/pontoon.service';
import { EmployeeService } from '../../services/employee.service';
import { AuthService } from '../../services/auth.service';
import { StockingService } from '../../services/stocking.service';
import { SpotEmployee } from '../../models/employee.model';
import { User } from '../../models/user.model';
import { FishStocking } from '../../models/stocking.model';
import { SpotStatistics } from '../../models/fishing-spot.model';
import * as L from 'leaflet';

@Component({
  selector: 'app-spot-manager',
  imports: [CommonModule, FormsModule],
  templateUrl: './spot-manager.html',
  styleUrl: './spot-manager.css',
  encapsulation: ViewEncapsulation.None
})
export class SpotManager implements OnInit, OnDestroy {
  spot: FishingSpot | null = null;
  managedSpots: FishingSpot[] = [];
  selectedManagedSpotId: number | null = null;
  pontoons: Pontoon[] = [];
  loading = true;
  notFound = false;
  isTouchDevice = false;
  
  showMessage = '';
  messageType: 'success' | 'error' = 'success';

  // Drawing state
  isDrawingMode = false;
  newPontoonName = '';
  newPontoonColor = '#3388ff';
  drawingPoints: L.LatLng[] = [];
  
  // Edit pontoon
  editingPontoonId: number | null = null;
  editPontoonName = '';
  editPontoonColor = '';

  // Zoom/center settings
  mapZoom = 18;
  mapCenterLat = 0;
  mapCenterLng = 0;

  // Spot details editing
  editDescription = '';
  fishSpeciesInput = '';
  fishSpeciesList: string[] = [];

  // Employee management
  spotEmployees: SpotEmployee[] = [];
  availableEmployees: User[] = [];
  selectedEmployeeId: number | null = null;
  loadingEmployees = false;

  // Sidebar tabs
  activeTab: 'dashboard' | 'pontoons' | 'settings' | 'stocking' = 'dashboard';

  get shouldShowMap(): boolean {
    return this.activeTab === 'pontoons' || this.activeTab === 'settings';
  }

  get activeTabLabel(): string {
    switch (this.activeTab) {
      case 'dashboard':
        return 'Dashboard';
      case 'pontoons':
        return 'Pontoons';
      case 'stocking':
        return 'Stocking';
      case 'settings':
        return 'Settings';
      default:
        return 'Dashboard';
    }
  }

  get chartBars(): { label: string; value: number; percent: number; color: string }[] {
    if (!this.statistics) return [];
    const items = [
      { label: 'Bookings', value: this.statistics.totalBookings, color: '#8cc45c' },
      { label: 'Active', value: this.statistics.activeBookings, color: '#4ecdc4' },
      { label: 'Cancelled', value: this.statistics.cancelledBookings, color: '#ff6b6b' },
      { label: 'Revenue', value: Math.round(this.statistics.totalRevenue), color: '#feca57' },
      { label: 'Pontoons', value: this.statistics.totalPontoons, color: '#45aaf2' },
      { label: 'Stockings', value: this.statistics.totalStockings, color: '#a55eea' },
    ];
    const max = Math.max(...items.map(i => i.value), 1);
    return items.map(i => ({ ...i, percent: (i.value / max) * 100 }));
  }

  // Statistics
  statistics: SpotStatistics | null = null;
  loadingStats = false;

  // Fish Stocking
  stockings: FishStocking[] = [];
  loadingStockings = false;
  newStockingSpecies = '';
  newStockingQuantity: number | null = null;
  newStockingDate = '';
  newStockingNotes = '';
  editingStockingId: number | null = null;
  editStockingSpecies = '';
  editStockingQuantity: number | null = null;
  editStockingDate = '';
  editStockingNotes = '';

  private map: L.Map | null = null;
  private pontoonLayers: Map<number, L.Polygon | L.Rectangle> = new Map();
  private drawingPolygon: L.Polygon | null = null;
  private drawingMarkers: L.CircleMarker[] = [];
  private editVertexMarkers: L.CircleMarker[] = [];
  private touchListeners: { el: HTMLElement; type: string; fn: EventListener }[] = [];
  private profileViewportPreview: L.Rectangle | null = null;
  private pendingMapRefreshFrame: number | null = null;
  private routeParamSubscription: Subscription | null = null;

  private readonly detailMapHeight = 220;
  private readonly detailPageDesktopMaxWidth = 1100;
  private readonly detailPageDesktopHorizontalPadding = 64;
  private readonly detailPageMobileHorizontalPadding = 32;
  private readonly detailBookingColumnWidth = 360;
  private readonly detailGridGap = 20;
  private readonly detailCardDesktopHorizontalPadding = 44;
  private readonly detailCardMobileHorizontalPadding = 32;

  readonly COLORS = [
    '#3388ff', '#ff6b6b', '#4ecdc4', '#feca57', 
    '#a55eea', '#26de81', '#fd9644', '#45aaf2'
  ];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fishingSpotService: FishingSpotService,
    private pontoonService: PontoonService,
    private employeeService: EmployeeService,
    private authService: AuthService,
    private stockingService: StockingService
  ) {}

  ngOnInit(): void {
    this.isTouchDevice = window.matchMedia('(pointer: coarse)').matches || navigator.maxTouchPoints > 0;

    if (!this.authService.isManagerOrAdmin()) {
      this.router.navigate(['/home']);
      return;
    }

    this.loadManagedSpots();

    this.routeParamSubscription = this.route.paramMap.subscribe(params => {
      const id = Number(params.get('id'));
      if (!Number.isFinite(id) || id <= 0) {
        this.loading = false;
        this.notFound = true;
        return;
      }

      this.loadSpot(id);
    });
  }

  ngOnDestroy(): void {
    this.routeParamSubscription?.unsubscribe();
    this.routeParamSubscription = null;

    if (this.pendingMapRefreshFrame !== null) {
      cancelAnimationFrame(this.pendingMapRefreshFrame);
      this.pendingMapRefreshFrame = null;
    }

    for (const listener of this.touchListeners) {
      listener.el.removeEventListener(listener.type, listener.fn);
    }
    this.touchListeners = [];

    this.removeProfileViewportPreview();
    if (this.map) {
      this.map.remove();
      this.map = null;
    }
  }

  private loadManagedSpots(): void {
    const userId = this.authService.getUserId();
    const isAdmin = this.authService.isAdmin();
    if (!isAdmin && !userId) return;

    this.fishingSpotService.getAll().subscribe({
      next: (spots) => {
        this.managedSpots = spots
          .filter(spot => isAdmin || spot.managerId === userId || spot.userId === userId)
          .sort((left, right) => left.name.localeCompare(right.name));

        if (this.spot) {
          this.selectedManagedSpotId = this.spot.id;
        }
      }
    });
  }

  private resetSpotState(): void {
    this.cancelEdit();
    this.isDrawingMode = false;
    this.drawingPoints = [];
    this.clearDrawingPreview();
    this.pontoons = [];
    this.statistics = null;
    this.stockings = [];
    this.spotEmployees = [];
    this.availableEmployees = [];
    this.loadingEmployees = false;
    this.loadingStats = false;
    this.loadingStockings = false;

    if (this.map) {
      this.map.remove();
      this.map = null;
    }

    this.pontoonLayers.clear();
    this.profileViewportPreview = null;
  }

  private loadSpot(id: number): void {
    this.loading = true;
    this.notFound = false;
    this.resetSpotState();

    this.fishingSpotService.getById(id).subscribe({
      next: (spot) => {
        this.spot = spot;
        this.selectedManagedSpotId = spot.id;
        this.loading = false;
        
        const userId = this.authService.getUserId();
        if (!this.authService.isAdmin() && spot.managerId !== userId && spot.userId !== userId) {
          this.router.navigate(['/home']);
          return;
        }
        
        this.editDescription = spot.description || '';
        this.fishSpeciesList = spot.fishSpecies ? JSON.parse(spot.fishSpecies) : [];

        setTimeout(() => {
          this.loadPontoons();
          this.loadEmployees();
          this.loadStatistics();
          this.loadStockings();

          if (this.shouldShowMap) {
            this.ensureMapReady();
          }
        }, 100);
      },
      error: () => {
        this.spot = null;
        this.loading = false;
        this.notFound = true;
      }
    });
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    if (!this.map) return;
    this.refreshMapLayout();
  }

  private loadPontoons(): void {
    if (!this.spot) return;
    this.pontoonService.getSpotPontoons(this.spot.id).subscribe({
      next: (pontoons) => {
        this.pontoons = pontoons;
        this.renderPontoons();
      }
    });
  }

  private initMap(): void {
    if (!this.spot || this.map) return;
    const el = document.getElementById('manager-map');
    if (!el) return;

    const centerLat = this.spot.defaultCenterLat ?? this.spot.latitude;
    const centerLng = this.spot.defaultCenterLng ?? this.spot.longitude;
    const zoom = this.spot.defaultZoom ?? 18;

    this.mapZoom = zoom;
    this.mapCenterLat = centerLat;
    this.mapCenterLng = centerLng;

    this.map = L.map('manager-map', {
      zoomControl: true,
      scrollWheelZoom: true,
      dragging: true,
      touchZoom: true,
      doubleClickZoom: false,
      attributionControl: false
    }).setView([centerLat, centerLng], zoom);

    L.tileLayer('https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}', {
      attribution: '© Google Maps',
      maxZoom: 22
    }).addTo(this.map);

    // Add spot marker
    const icon = L.divIcon({
      html: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 68" width="36" height="51">
        <ellipse cx="24" cy="64" rx="8" ry="4" fill="rgba(0,0,0,0.25)"/>
        <path d="M24 2C13.5 2 5 10.5 5 21c0 14 19 43 19 43S43 35 43 21C43 10.5 34.5 2 24 2z" fill="#7db34a" stroke="#fff" stroke-width="2"/>
        <circle cx="24" cy="21" r="7" fill="rgba(255,255,255,0.9)"/>
      </svg>`,
      className: '',
      iconSize: [36, 51],
      iconAnchor: [18, 51],
    });
    L.marker([this.spot.latitude, this.spot.longitude], { icon }).addTo(this.map);

    // Drawing click handler
    this.map.on('click', (e: L.LeafletMouseEvent) => this.onMapClick(e));
    this.map.on('move', () => this.syncProfileViewportPreview());
    this.map.on('zoom', () => this.syncProfileViewportPreview());
    this.renderPontoons();
    this.refreshMapLayout();
    this.syncProfileViewportPreview();
  }

  private ensureMapReady(): void {
    if (!this.map) {
      this.initMap();
      return;
    }

    this.refreshMapLayout();
  }

  private refreshMapLayout(): void {
    if (!this.map) return;

    if (this.pendingMapRefreshFrame !== null) {
      cancelAnimationFrame(this.pendingMapRefreshFrame);
      this.pendingMapRefreshFrame = null;
    }

    this.pendingMapRefreshFrame = requestAnimationFrame(() => {
      this.pendingMapRefreshFrame = requestAnimationFrame(() => {
        if (!this.map) return;

        this.map.invalidateSize({ pan: false, debounceMoveend: true });
        this.syncProfileViewportPreview();
        this.pendingMapRefreshFrame = null;
      });
    });
  }

  // ---- Rendering ----

  private renderPontoons(): void {
    if (!this.map) return;

    this.pontoonLayers.forEach(layer => layer.remove());
    this.pontoonLayers.clear();

    this.pontoons.forEach(pontoon => {
      let layer: L.Polygon | L.Rectangle;

      if (pontoon.coordinates) {
        // Polygon pontoon
        const coords: [number, number][] = JSON.parse(pontoon.coordinates);
        layer = L.polygon(coords.map(c => L.latLng(c[0], c[1])), {
          color: pontoon.color || '#3388ff',
          weight: 2,
          fillOpacity: 0.35,
          interactive: true
        }).addTo(this.map!);
      } else {
        // Legacy rectangle pontoon
        const bounds: L.LatLngBoundsExpression = [
          [pontoon.southWestLat, pontoon.southWestLng],
          [pontoon.northEastLat, pontoon.northEastLng]
        ];
        layer = L.rectangle(bounds, {
          color: pontoon.color || '#3388ff',
          weight: 2,
          fillOpacity: 0.35,
          interactive: true
        }).addTo(this.map!);
      }

      layer.bindTooltip(pontoon.name, { permanent: false, direction: 'center' });
      layer.on('click', (e: L.LeafletMouseEvent) => {
        L.DomEvent.stopPropagation(e);
        this.selectPontoon(pontoon);
      });
      this.pontoonLayers.set(pontoon.id, layer);
    });

    this.syncProfileViewportPreview();
  }

  setActiveTab(tab: 'dashboard' | 'pontoons' | 'settings' | 'stocking'): void {
    this.activeTab = tab;

    if (this.shouldShowMap) {
      this.ensureMapReady();
      return;
    }

    this.syncProfileViewportPreview();
  }

  private syncProfileViewportPreview(): void {
    if (!this.map) return;

    if (this.activeTab !== 'settings') {
      this.removeProfileViewportPreview();
      return;
    }

    const previewBounds = this.getProfileViewportBounds();
    if (!previewBounds) return;

    if (!this.profileViewportPreview) {
      this.profileViewportPreview = L.rectangle(previewBounds, {
        color: '#fbbf24',
        weight: 2,
        dashArray: '10 6',
        opacity: 0.95,
        fillColor: '#fbbf24',
        fillOpacity: 0.05,
        interactive: false
      }).addTo(this.map);
    } else {
      this.profileViewportPreview.setBounds(previewBounds);
      if (!this.map.hasLayer(this.profileViewportPreview)) {
        this.profileViewportPreview.addTo(this.map);
      }
    }

    this.profileViewportPreview.bringToFront();
  }

  private removeProfileViewportPreview(): void {
    if (!this.profileViewportPreview) return;
    this.profileViewportPreview.remove();
  }

  private getProfileViewportBounds(): L.LatLngBounds | null {
    if (!this.map) return null;

    const viewport = this.getProfileMapViewportSize();
    const mapSize = this.map.getSize();
    if (mapSize.x === 0 || mapSize.y === 0) return null;

    const centerPoint = L.point(mapSize.x / 2, mapSize.y / 2);
    const halfWidth = viewport.width / 2;
    const halfHeight = viewport.height / 2;

    const northWest = this.map.containerPointToLatLng(L.point(centerPoint.x - halfWidth, centerPoint.y - halfHeight));
    const southEast = this.map.containerPointToLatLng(L.point(centerPoint.x + halfWidth, centerPoint.y + halfHeight));

    return L.latLngBounds(northWest, southEast);
  }

  private getProfileMapViewportSize(): { width: number; height: number } {
    const viewportWidth = window.innerWidth;

    if (viewportWidth <= 820) {
      const detailPageWidth = Math.max(300, viewportWidth - this.detailPageMobileHorizontalPadding);
      return {
        width: Math.max(220, detailPageWidth - this.detailCardMobileHorizontalPadding),
        height: this.detailMapHeight
      };
    }

    const detailPageWidth = Math.min(viewportWidth, this.detailPageDesktopMaxWidth) - this.detailPageDesktopHorizontalPadding;
    const infoColumnWidth = detailPageWidth - this.detailBookingColumnWidth - this.detailGridGap;

    return {
      width: Math.max(240, infoColumnWidth - this.detailCardDesktopHorizontalPadding),
      height: this.detailMapHeight
    };
  }

  // ---- Drawing Mode ----

  toggleDrawingMode(): void {
    this.isDrawingMode = !this.isDrawingMode;
    this.cancelEdit();

    if (this.isDrawingMode) {
      if (this.map) {
        (this.map.getContainer() as HTMLElement).style.cursor = 'crosshair';
      }
      this.drawingPoints = [];
      this.clearDrawingPreview();
    } else {
      if (this.map) {
        (this.map.getContainer() as HTMLElement).style.cursor = '';
      }
      this.drawingPoints = [];
      this.clearDrawingPreview();
      this.pontoonLayers.forEach(layer => {
        layer.setStyle({ weight: 2, dashArray: undefined });
      });
    }
  }

  private onMapClick(e: L.LeafletMouseEvent): void {
    if (!this.map) return;

    if (!this.isDrawingMode) {
      if (this.editingPontoonId !== null) {
        if (this.isLatLngWithinSelectedPontoon(e.latlng)) {
          return;
        }

        this.cancelEdit();
      }
      return;
    }

    const clickLatLng = e.latlng;

    // Check if clicking near the first point to close the polygon
    if (this.drawingPoints.length >= 3) {
      const firstPoint = this.map.latLngToContainerPoint(this.drawingPoints[0]);
      const clickPoint = this.map.latLngToContainerPoint(clickLatLng);
      const dist = firstPoint.distanceTo(clickPoint);
      if (dist < 15) {
        this.finishDrawing();
        return;
      }
    }

    this.drawingPoints.push(clickLatLng);
    this.updateDrawingPreview();
  }

  private updateDrawingPreview(): void {
    if (!this.map) return;
    this.clearDrawingPreview();

    if (this.drawingPoints.length === 0) return;

    // Draw vertex markers
    this.drawingPoints.forEach((pt, i) => {
      const marker = L.circleMarker(pt, {
        radius: i === 0 && this.drawingPoints.length >= 3 ? 8 : 5,
        color: i === 0 ? '#fff' : this.newPontoonColor,
        fillColor: i === 0 ? this.newPontoonColor : '#fff',
        fillOpacity: 1,
        weight: 2,
        interactive: false
      }).addTo(this.map!);
      this.drawingMarkers.push(marker);
    });

    // Draw polygon preview
    if (this.drawingPoints.length >= 2) {
      this.drawingPolygon = L.polygon(this.drawingPoints, {
        color: this.newPontoonColor,
        weight: 2,
        fillOpacity: 0.25,
        dashArray: '6, 4',
        interactive: false
      }).addTo(this.map);
    }
  }

  private clearDrawingPreview(): void {
    this.drawingMarkers.forEach(m => m.remove());
    this.drawingMarkers = [];
    if (this.drawingPolygon) {
      this.drawingPolygon.remove();
      this.drawingPolygon = null;
    }
  }

  undoLastPoint(): void {
    if (this.drawingPoints.length === 0) return;
    this.drawingPoints.pop();
    this.updateDrawingPreview();
  }

  finishDrawing(): void {
    if (this.drawingPoints.length < 3 || !this.spot) {
      this.showToast('At least 3 points are needed to create a pontoon', 'error');
      return;
    }

    const coords: [number, number][] = this.drawingPoints.map(p => [p.lat, p.lng]);
    
    // Calculate bounding box for backward compat
    const lats = coords.map(c => c[0]);
    const lngs = coords.map(c => c[1]);

    const pontoonData: CreatePontoon = {
      fishingSpotId: this.spot.id,
      name: this.newPontoonName || `Pontoon ${this.pontoons.length + 1}`,
      southWestLat: Math.min(...lats),
      southWestLng: Math.min(...lngs),
      northEastLat: Math.max(...lats),
      northEastLng: Math.max(...lngs),
      color: this.newPontoonColor,
      coordinates: JSON.stringify(coords)
    };

    this.pontoonService.createPontoon(pontoonData).subscribe({
      next: () => {
        this.showToast('Pontoon created!', 'success');
        this.loadPontoons();
        this.newPontoonName = '';
        this.toggleDrawingMode();
      },
      error: () => {
        this.showToast('Error creating pontoon', 'error');
      }
    });

    this.clearDrawingPreview();
    this.drawingPoints = [];
  }

  // ---- Editing ----

  selectPontoon(pontoon: Pontoon): void {
    if (this.isDrawingMode) return;
    
    this.editingPontoonId = pontoon.id;
    this.editPontoonName = pontoon.name;
    this.editPontoonColor = pontoon.color || '#3388ff';

    // Highlight selected
    this.pontoonLayers.forEach((layer, id) => {
      if (id === pontoon.id) {
        layer.setStyle({ weight: 4, dashArray: '5, 5' });
      } else {
        layer.setStyle({ weight: 2, dashArray: undefined });
      }
    });

    // Show vertex markers for editing
    this.showEditVertexMarkers(pontoon);
  }

  private showEditVertexMarkers(pontoon: Pontoon): void {
    this.clearEditVertexMarkers();
    if (!this.map) return;

    let coords: L.LatLng[];
    const layer = this.pontoonLayers.get(pontoon.id);
    if (!layer) return;

    if (pontoon.coordinates) {
      const parsed: [number, number][] = JSON.parse(pontoon.coordinates);
      coords = parsed.map(c => L.latLng(c[0], c[1]));
    } else {
      // Rectangle — get 4 corners
      const bounds = (layer as L.Rectangle).getBounds();
      const sw = bounds.getSouthWest();
      const ne = bounds.getNorthEast();
      coords = [
        L.latLng(sw.lat, sw.lng),
        L.latLng(sw.lat, ne.lng),
        L.latLng(ne.lat, ne.lng),
        L.latLng(ne.lat, sw.lng)
      ];
    }

    coords.forEach((pt, idx) => {
      const marker = L.circleMarker(pt, {
        radius: 6,
        color: '#fff',
        fillColor: pontoon.color || '#3388ff',
        fillOpacity: 1,
        weight: 2,
        interactive: true,
        className: 'vertex-marker'
      }).addTo(this.map!);

      this.makeVertexDraggable(marker, idx, pontoon);
      this.editVertexMarkers.push(marker);
    });
  }

  private makeVertexDraggable(marker: L.CircleMarker, index: number, pontoon: Pontoon): void {
    let dragging = false;

    marker.on('click', (e: L.LeafletMouseEvent) => {
      L.DomEvent.stopPropagation(e);
    });

    marker.on('mousedown', (e: L.LeafletMouseEvent) => {
      L.DomEvent.stopPropagation(e);
      (e as any).originalEvent?.preventDefault();
      dragging = true;
      this.map!.dragging.disable();

      const onMove = (me: L.LeafletMouseEvent) => {
        if (!dragging) return;
        marker.setLatLng(me.latlng);
        this.updatePolygonFromVertices(pontoon);
      };

      const onUp = () => {
        if (!dragging) return;
        dragging = false;
        this.map!.dragging.enable();
        this.map!.off('mousemove', onMove as any);
        this.map!.off('mouseup', onUp);
        this.saveVertexPositions(pontoon);
      };

      this.map!.on('mousemove', onMove as any);
      this.map!.on('mouseup', onUp);
    });

    // Touch support
    marker.on('touchstart' as any, (e: any) => {
      L.DomEvent.stopPropagation(e);
      if (e.originalEvent) e.originalEvent.preventDefault();
      dragging = true;
      this.map!.dragging.disable();
    });

    const touchMoveHandler = (te: TouchEvent) => {
      if (!dragging) return;
      te.preventDefault();
      const touch = te.touches[0];
      const containerRect = this.map!.getContainer().getBoundingClientRect();
      const containerPoint = L.point(
        touch.clientX - containerRect.left,
        touch.clientY - containerRect.top
      );
      const latLng = this.map!.containerPointToLatLng(containerPoint);
      marker.setLatLng(latLng);
      this.updatePolygonFromVertices(pontoon);
    };

    const touchEndHandler = () => {
      if (!dragging) return;
      dragging = false;
      this.map!.dragging.enable();
      this.saveVertexPositions(pontoon);
    };

    const container = this.map!.getContainer();
    container.addEventListener('touchmove', touchMoveHandler, { passive: false });
    container.addEventListener('touchend', touchEndHandler);
    this.touchListeners.push(
      { el: container, type: 'touchmove', fn: touchMoveHandler as EventListener },
      { el: container, type: 'touchend', fn: touchEndHandler as EventListener }
    );
  }

  private updatePolygonFromVertices(pontoon: Pontoon): void {
    const layer = this.pontoonLayers.get(pontoon.id);
    if (!layer || !(layer instanceof L.Polygon)) return;

    const newCoords = this.editVertexMarkers.map(m => m.getLatLng());
    (layer as L.Polygon).setLatLngs(newCoords);
  }

  private saveVertexPositions(pontoon: Pontoon): void {
    const coords: [number, number][] = this.editVertexMarkers.map(m => {
      const ll = m.getLatLng();
      return [ll.lat, ll.lng];
    });

    const lats = coords.map(c => c[0]);
    const lngs = coords.map(c => c[1]);

    this.pontoonService.updatePontoon(pontoon.id, {
      coordinates: JSON.stringify(coords),
      southWestLat: Math.min(...lats),
      southWestLng: Math.min(...lngs),
      northEastLat: Math.max(...lats),
      northEastLng: Math.max(...lngs)
    }).subscribe({
      next: () => {
        this.showToast('Shape updated!', 'success');
        // Update local data
        const p = this.pontoons.find(pp => pp.id === pontoon.id);
        if (p) p.coordinates = JSON.stringify(coords);
      },
      error: () => {
        this.showToast('Error saving shape', 'error');
        this.loadPontoons();
      }
    });
  }

  private clearEditVertexMarkers(): void {
    this.editVertexMarkers.forEach(m => m.remove());
    this.editVertexMarkers = [];
    for (const listener of this.touchListeners) {
      listener.el.removeEventListener(listener.type, listener.fn);
    }
    this.touchListeners = [];
  }

  private isLatLngWithinSelectedPontoon(latLng: L.LatLng): boolean {
    if (!this.map || this.editingPontoonId === null) return false;

    const layer = this.pontoonLayers.get(this.editingPontoonId);
    if (!layer) return false;

    if (layer instanceof L.Rectangle) {
      return layer.getBounds().contains(latLng);
    }

    if (!(layer instanceof L.Polygon)) {
      return false;
    }

    const rawLatLngs = layer.getLatLngs();
    const polygonPoints = Array.isArray(rawLatLngs[0])
      ? rawLatLngs[0] as L.LatLng[]
      : rawLatLngs as L.LatLng[];

    return this.isLatLngOnPolygonEdge(latLng, polygonPoints) || this.isLatLngInPolygon(latLng, polygonPoints);
  }

  private isLatLngOnPolygonEdge(latLng: L.LatLng, polygonPoints: L.LatLng[]): boolean {
    if (!this.map || polygonPoints.length < 2) return false;

    const target = this.map.latLngToContainerPoint(latLng);
    for (let index = 0; index < polygonPoints.length; index++) {
      const start = this.map.latLngToContainerPoint(polygonPoints[index]);
      const end = this.map.latLngToContainerPoint(polygonPoints[(index + 1) % polygonPoints.length]);
      if (L.LineUtil.pointToSegmentDistance(target, start, end) <= 8) {
        return true;
      }
    }

    return false;
  }

  private isLatLngInPolygon(latLng: L.LatLng, polygonPoints: L.LatLng[]): boolean {
    if (polygonPoints.length < 3) return false;

    let isInside = false;
    const targetLat = latLng.lat;
    const targetLng = latLng.lng;

    for (let index = 0, previousIndex = polygonPoints.length - 1; index < polygonPoints.length; previousIndex = index++) {
      const currentPoint = polygonPoints[index];
      const previousPoint = polygonPoints[previousIndex];

      const intersects = ((currentPoint.lat > targetLat) !== (previousPoint.lat > targetLat))
        && (targetLng < ((previousPoint.lng - currentPoint.lng) * (targetLat - currentPoint.lat))
          / ((previousPoint.lat - currentPoint.lat) || Number.EPSILON) + currentPoint.lng);

      if (intersects) {
        isInside = !isInside;
      }
    }

    return isInside;
  }

  updatePontoon(): void {
    if (!this.editingPontoonId) return;

    this.pontoonService.updatePontoon(this.editingPontoonId, {
      name: this.editPontoonName,
      color: this.editPontoonColor
    }).subscribe({
      next: () => {
        this.showToast('Pontoon updated!', 'success');
        this.loadPontoons();
        this.cancelEdit();
      },
      error: () => {
        this.showToast('Error updating pontoon', 'error');
      }
    });
  }

  deletePontoon(): void {
    if (!this.editingPontoonId) return;
    if (!confirm('Are you sure you want to delete this pontoon?')) return;

    this.pontoonService.deletePontoon(this.editingPontoonId).subscribe({
      next: () => {
        this.showToast('Pontoon deleted!', 'success');
        this.loadPontoons();
        this.cancelEdit();
      },
      error: () => {
        this.showToast('Error deleting pontoon', 'error');
      }
    });
  }

  cancelEdit(): void {
    this.editingPontoonId = null;
    this.editPontoonName = '';
    this.editPontoonColor = '';
    this.clearEditVertexMarkers();

    if (this.map) {
      (this.map.getContainer() as HTMLElement).style.cursor = '';
    }

    this.pontoonLayers.forEach(layer => {
      layer.setStyle({ weight: 2, dashArray: undefined });
    });
  }

  // ---- Zoom/Center Settings ----

  saveMapView(): void {
    if (!this.map || !this.spot) return;
    const center = this.map.getCenter();
    const zoom = this.map.getZoom();

    this.fishingSpotService.update(this.spot.id, {
      defaultZoom: zoom,
      defaultCenterLat: center.lat,
      defaultCenterLng: center.lng
    }).subscribe({
      next: () => {
        this.spot!.defaultZoom = zoom;
        this.spot!.defaultCenterLat = center.lat;
        this.spot!.defaultCenterLng = center.lng;
        this.mapZoom = zoom;
        this.mapCenterLat = center.lat;
        this.mapCenterLng = center.lng;
        this.showToast('Map view saved! Users will see this default view.', 'success');
      },
      error: () => {
        this.showToast('Error saving map view', 'error');
      }
    });
  }

  resetMapView(): void {
    if (!this.map || !this.spot) return;
    this.map.setView([this.spot.latitude, this.spot.longitude], 18);
    
    this.fishingSpotService.update(this.spot.id, {
      resetDefaultMapView: true
    }).subscribe({
      next: () => {
        this.spot!.defaultZoom = undefined;
        this.spot!.defaultCenterLat = undefined;
        this.spot!.defaultCenterLng = undefined;
        this.showToast('Map view reset to default', 'success');
      },
      error: () => {
        this.showToast('Error resetting map view', 'error');
      }
    });
  }

  // ---- Spot Details ----

  saveDescription(): void {
    if (!this.spot) return;
    this.fishingSpotService.update(this.spot.id, {
      description: this.editDescription
    }).subscribe({
      next: () => {
        this.spot!.description = this.editDescription;
        this.showToast('Description saved!', 'success');
      },
      error: () => this.showToast('Error saving description', 'error')
    });
  }

  addFishSpecies(): void {
    const species = this.fishSpeciesInput.trim();
    if (!species || !this.spot) return;
    if (this.fishSpeciesList.some(s => s.toLowerCase() === species.toLowerCase())) {
      this.showToast('Species already added', 'error');
      return;
    }
    this.fishSpeciesList.push(species);
    this.fishSpeciesInput = '';
    this.saveFishSpecies();
  }

  removeFishSpecies(index: number): void {
    this.fishSpeciesList.splice(index, 1);
    this.saveFishSpecies();
  }

  private saveFishSpecies(): void {
    if (!this.spot) return;
    const json = JSON.stringify(this.fishSpeciesList);
    this.fishingSpotService.update(this.spot.id, {
      fishSpecies: json
    }).subscribe({
      next: () => {
        this.spot!.fishSpecies = json;
        this.showToast('Fish species updated!', 'success');
      },
      error: () => this.showToast('Error saving fish species', 'error')
    });
  }

  goBack(): void {
    if (this.spot) {
      this.router.navigate(['/spots', this.spot.id]);
      return;
    }

    this.router.navigate(['/profile']);
  }

  changeManagedSpot(spotId: number | string | null): void {
    const nextSpotId = Number(spotId);
    if (!Number.isFinite(nextSpotId) || !nextSpotId || nextSpotId === this.spot?.id) {
      this.selectedManagedSpotId = this.spot?.id ?? null;
      return;
    }

    this.router.navigate(['/spots', nextSpotId, 'manage']);
  }

  // ---- Employee Management ----

  loadEmployees(): void {
    if (!this.spot) return;
    this.loadingEmployees = true;
    this.employeeService.getSpotEmployees(this.spot.id).subscribe({
      next: (employees) => {
        this.spotEmployees = employees;
        this.loadingEmployees = false;
      },
      error: () => {
        this.loadingEmployees = false;
      }
    });
    this.employeeService.getAvailableEmployees().subscribe({
      next: (employees) => {
        this.availableEmployees = employees;
      }
    });
  }

  assignEmployee(): void {
    if (!this.selectedEmployeeId || !this.spot) return;
    this.employeeService.assignEmployee({
      userId: this.selectedEmployeeId,
      fishingSpotId: this.spot.id
    }).subscribe({
      next: () => {
        this.showToast('Employee assigned successfully!', 'success');
        this.selectedEmployeeId = null;
        this.loadEmployees();
      },
      error: () => {
        this.showToast('Error assigning employee', 'error');
      }
    });
  }

  removeSpotEmployee(id: number): void {
    if (!confirm('Are you sure you want to remove this employee from the spot?')) return;
    this.employeeService.removeEmployee(id).subscribe({
      next: () => {
        this.showToast('Employee removed!', 'success');
        this.loadEmployees();
      },
      error: () => {
        this.showToast('Error removing employee', 'error');
      }
    });
  }

  // ---- Statistics ----

  loadStatistics(): void {
    if (!this.spot) return;
    this.loadingStats = true;
    this.stockingService.getStatistics(this.spot.id).subscribe({
      next: (stats) => {
        this.statistics = stats;
        this.loadingStats = false;
      },
      error: () => {
        this.loadingStats = false;
      }
    });
  }

  // ---- Fish Stocking ----

  loadStockings(): void {
    if (!this.spot) return;
    this.loadingStockings = true;
    this.stockingService.getStockings(this.spot.id).subscribe({
      next: (stockings) => {
        this.stockings = stockings;
        this.loadingStockings = false;
      },
      error: () => {
        this.loadingStockings = false;
      }
    });
  }

  addStocking(): void {
    if (!this.spot || !this.newStockingSpecies.trim() || !this.newStockingQuantity || !this.newStockingDate) return;
    this.stockingService.createStocking(this.spot.id, {
      species: this.newStockingSpecies.trim(),
      quantity: this.newStockingQuantity,
      stockingDate: this.newStockingDate,
      notes: this.newStockingNotes.trim() || undefined
    }).subscribe({
      next: () => {
        this.showToast('Stocking added!', 'success');
        this.newStockingSpecies = '';
        this.newStockingQuantity = null;
        this.newStockingDate = '';
        this.newStockingNotes = '';
        this.loadStockings();
        this.loadStatistics();
      },
      error: () => this.showToast('Error adding stocking', 'error')
    });
  }

  startEditStocking(s: FishStocking): void {
    this.editingStockingId = s.id;
    this.editStockingSpecies = s.species;
    this.editStockingQuantity = s.quantity;
    this.editStockingDate = s.stockingDate.substring(0, 10);
    this.editStockingNotes = s.notes || '';
  }

  cancelEditStocking(): void {
    this.editingStockingId = null;
  }

  saveEditStocking(): void {
    if (!this.spot || !this.editingStockingId) return;
    this.stockingService.updateStocking(this.spot.id, this.editingStockingId, {
      species: this.editStockingSpecies.trim(),
      quantity: this.editStockingQuantity!,
      stockingDate: this.editStockingDate,
      notes: this.editStockingNotes.trim() || undefined
    }).subscribe({
      next: () => {
        this.showToast('Stocking updated!', 'success');
        this.editingStockingId = null;
        this.loadStockings();
      },
      error: () => this.showToast('Error updating stocking', 'error')
    });
  }

  deleteStocking(id: number): void {
    if (!this.spot || !confirm('Delete this stocking record?')) return;
    this.stockingService.deleteStocking(this.spot.id, id).subscribe({
      next: () => {
        this.showToast('Stocking deleted!', 'success');
        this.loadStockings();
        this.loadStatistics();
      },
      error: () => this.showToast('Error deleting stocking', 'error')
    });
  }

  private showToast(msg: string, type: 'success' | 'error'): void {
    this.showMessage = msg;
    this.messageType = type;
    setTimeout(() => this.showMessage = '', 3000);
  }
}
