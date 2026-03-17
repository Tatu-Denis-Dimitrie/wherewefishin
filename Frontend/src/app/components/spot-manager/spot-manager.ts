import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { FishingSpotService, FishingSpot } from '../../services/fishing-spot.service';
import { PontoonService, Pontoon, CreatePontoon } from '../../services/pontoon.service';
import { AuthService } from '../../services/auth.service';
import * as L from 'leaflet';

@Component({
  selector: 'app-spot-manager',
  imports: [CommonModule, FormsModule],
  templateUrl: './spot-manager.html',
  styleUrl: './spot-manager.css'
})
export class SpotManager implements OnInit, OnDestroy {
  spot: FishingSpot | null = null;
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
  
  // Edit pontoon
  editingPontoonId: number | null = null;
  editPontoonName = '';
  editPontoonColor = '';

  // Drag state (auto-active when pontoon is selected)
  private _isDragging = false;
  private repositionDragStart: L.LatLng | null = null;
  private repositionOrigBounds: L.LatLngBounds | null = null;
  private dragTouchId: number | null = null;

  private readonly containerTouchStartHandler = (event: TouchEvent) => this.onContainerTouchStart(event);
  private readonly containerTouchMoveHandler = (event: TouchEvent) => this.onContainerTouchMove(event);
  private readonly containerTouchEndHandler = (event: TouchEvent) => this.onContainerTouchEnd(event);

  private map: L.Map | null = null;
  private pontoonLayers: Map<number, L.Rectangle> = new Map();
  private drawingRect: L.Rectangle | null = null;
  private startLatLng: L.LatLng | null = null;

  readonly COLORS = [
    '#3388ff', '#ff6b6b', '#4ecdc4', '#feca57', 
    '#a55eea', '#26de81', '#fd9644', '#45aaf2'
  ];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private fishingSpotService: FishingSpotService,
    private pontoonService: PontoonService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.isTouchDevice = window.matchMedia('(pointer: coarse)').matches || navigator.maxTouchPoints > 0;

    if (!this.authService.isManagerOrAdmin()) {
      this.router.navigate(['/home']);
      return;
    }

    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.fishingSpotService.getById(id).subscribe({
      next: (spot) => {
        this.spot = spot;
        this.loading = false;
        
        // Check if user has permission
        const userId = this.authService.getUserId();
        if (!this.authService.isAdmin() && spot.managerId !== userId && spot.userId !== userId) {
          this.router.navigate(['/home']);
          return;
        }
        
        setTimeout(() => {
          this.initMap();
          this.loadPontoons();
        }, 100);
      },
      error: () => {
        this.loading = false;
        this.notFound = true;
      }
    });
  }

  ngOnDestroy(): void {
    if (this.map) {
      if (this.isTouchDevice) {
        const container = this.map.getContainer();
        container.removeEventListener('touchstart', this.containerTouchStartHandler);
        container.removeEventListener('touchmove', this.containerTouchMoveHandler);
        container.removeEventListener('touchend', this.containerTouchEndHandler);
        container.removeEventListener('touchcancel', this.containerTouchEndHandler);
      }

      this.map.remove();
      this.map = null;
    }
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
    if (!this.spot) return;
    const el = document.getElementById('manager-map');
    if (!el) return;

    this.map = L.map('manager-map', {
      zoomControl: true,
      scrollWheelZoom: true,
      dragging: !this.isTouchDevice,
      touchZoom: true,
      doubleClickZoom: false,
      attributionControl: false
    }).setView([this.spot.latitude, this.spot.longitude], 18);

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

    // Setup drawing events
    this.map.on('mousedown', (e: L.LeafletMouseEvent) => this.onMapMouseDown(e));
    this.map.on('mousemove', (e: L.LeafletMouseEvent) => this.onMapMouseMove(e));
    this.map.on('mouseup', () => this.onMapMouseUp());

    if (this.isTouchDevice) {
      this.map.on('click', (e: L.LeafletMouseEvent) => this.onMapTapToDraw(e));

      const container = this.map.getContainer();
      container.addEventListener('touchstart', this.containerTouchStartHandler, { passive: false });
      container.addEventListener('touchmove', this.containerTouchMoveHandler, { passive: false });
      container.addEventListener('touchend', this.containerTouchEndHandler, { passive: false });
      container.addEventListener('touchcancel', this.containerTouchEndHandler, { passive: false });
    }
  }

  private renderPontoons(): void {
    if (!this.map) return;

    // Clear existing
    this.pontoonLayers.forEach(layer => layer.remove());
    this.pontoonLayers.clear();

    // Add pontoons
    this.pontoons.forEach(pontoon => {
      const bounds: L.LatLngBoundsExpression = [
        [pontoon.southWestLat, pontoon.southWestLng],
        [pontoon.northEastLat, pontoon.northEastLng]
      ];
      const rect = L.rectangle(bounds, {
        color: pontoon.color || '#3388ff',
        weight: 2,
        fillOpacity: 0.35,
        interactive: true
      }).addTo(this.map!);

      rect.bindTooltip(pontoon.name, { permanent: false, direction: 'center' });
      rect.on('click', () => this.selectPontoon(pontoon));

      // Repositioning drag events
      rect.on('mousedown', (e: L.LeafletMouseEvent) => this.onPontoonDragStart(e, pontoon.id));
      rect.on('touchstart', (e: L.LeafletEvent) => this.onPontoonTouchStart(e, pontoon.id));

      this.pontoonLayers.set(pontoon.id, rect);
    });
  }

  toggleDrawingMode(): void {
    this.isDrawingMode = !this.isDrawingMode;
    this.editingPontoonId = null;
    this._isDragging = false;
    this.repositionDragStart = null;
    this.repositionOrigBounds = null;
    this.dragTouchId = null;

    if (this.map) {
      if (this.isDrawingMode) {
        this.map.dragging.disable();
        (this.map.getContainer() as HTMLElement).style.cursor = 'crosshair';
      } else {
        if (this.isTouchDevice) {
          this.map.dragging.disable();
        } else {
          this.map.dragging.enable();
        }
        (this.map.getContainer() as HTMLElement).style.cursor = '';
      }
    }
  }

  private onMapMouseDown(e: L.LeafletMouseEvent): void {
    if (this.isTouchDevice || !this.isDrawingMode || !this.map) return;

    this.startLatLng = e.latlng;
    const bounds = L.latLngBounds(e.latlng, e.latlng);
    this.drawingRect = L.rectangle(bounds, {
      color: this.newPontoonColor,
      weight: 2,
      fillOpacity: 0.35,
      dashArray: '5, 5'
    }).addTo(this.map);
  }

  private onMapMouseMove(e: L.LeafletMouseEvent): void {
    // Handle repositioning drag
    if (this._isDragging) {
      this.applyRepositionFromLatLng(e.latlng);
      return;
    }

    // Handle drawing drag
    if (!this.isDrawingMode || !this.drawingRect || !this.startLatLng) return;
    this.drawingRect.setBounds(L.latLngBounds(this.startLatLng, e.latlng));
  }

  private onMapMouseUp(): void {
    // Handle repositioning end
    if (this._isDragging) {
      this.finishRepositionDrag();
      return;
    }

    // Handle drawing end with mouse (desktop)
    if (this.isTouchDevice) return;
    this.finalizeDrawingRect();
  }

  private onMapTapToDraw(e: L.LeafletMouseEvent): void {
    if (!this.isTouchDevice || !this.isDrawingMode || !this.map) return;

    if (!this.startLatLng) {
      this.startLatLng = e.latlng;
      if (this.drawingRect) {
        this.drawingRect.remove();
      }

      this.drawingRect = L.rectangle(L.latLngBounds(e.latlng, e.latlng), {
        color: this.newPontoonColor,
        weight: 2,
        fillOpacity: 0.35,
        dashArray: '5, 5'
      }).addTo(this.map);

      this.showToast('Tap the second corner to finish the pontoon', 'success');
      return;
    }

    if (this.drawingRect) {
      this.drawingRect.setBounds(L.latLngBounds(this.startLatLng, e.latlng));
    }
    this.finalizeDrawingRect();
  }

  private finalizeDrawingRect(): void {
    if (!this.isDrawingMode || !this.drawingRect || !this.startLatLng || !this.spot) return;

    const bounds = this.drawingRect.getBounds();
    const sw = bounds.getSouthWest();
    const ne = bounds.getNorthEast();

    // Check if it's a valid rectangle (not just a click)
    if (Math.abs(sw.lat - ne.lat) < 0.00001 || Math.abs(sw.lng - ne.lng) < 0.00001) {
      this.drawingRect.remove();
      this.drawingRect = null;
      this.startLatLng = null;
      this.showToast('Select a larger area for the pontoon', 'error');
      return;
    }

    const pontoonData: CreatePontoon = {
      fishingSpotId: this.spot.id,
      name: this.newPontoonName || `Pontoon ${this.pontoons.length + 1}`,
      southWestLat: sw.lat,
      southWestLng: sw.lng,
      northEastLat: ne.lat,
      northEastLng: ne.lng,
      color: this.newPontoonColor
    };

    this.pontoonService.createPontoon(pontoonData).subscribe({
      next: () => {
        this.showToast('Pontoon added successfully!', 'success');
        this.loadPontoons();
        this.newPontoonName = '';
        this.toggleDrawingMode();
      },
      error: () => {
        this.showToast('Error adding pontoon', 'error');
      }
    });

    this.drawingRect.remove();
    this.drawingRect = null;
    this.startLatLng = null;
  }

  // ---------- Repositioning ----------

  private onPontoonDragStart(e: L.LeafletMouseEvent, pontoonId: number): void {
    if (this.isTouchDevice || this.editingPontoonId !== pontoonId || this.isDrawingMode) return;

    L.DomEvent.stopPropagation(e);
    this.beginReposition(pontoonId, e.latlng);
  }

  private onPontoonTouchStart(e: L.LeafletEvent, pontoonId: number): void {
    if (!this.isTouchDevice || this.editingPontoonId !== pontoonId || this.isDrawingMode) return;

    const originalEvent = (e as L.LeafletEvent & { originalEvent?: TouchEvent }).originalEvent;
    if (!originalEvent || originalEvent.touches.length === 0) return;

    L.DomEvent.stopPropagation(e);
    originalEvent.preventDefault();

    this.dragTouchId = originalEvent.touches[0].identifier;
    const startLatLng = this.touchToLatLng(originalEvent.touches[0]);
    this.beginReposition(pontoonId, startLatLng);
  }

  private beginReposition(pontoonId: number, startLatLng: L.LatLng): void {
    const layer = this.pontoonLayers.get(pontoonId);
    if (!layer) return;

    this._isDragging = true;
    this.repositionDragStart = startLatLng;
    this.repositionOrigBounds = L.latLngBounds(
      layer.getBounds().getSouthWest(),
      layer.getBounds().getNorthEast()
    );

    if (this.map) {
      this.map.dragging.disable();
    }
  }

  private applyRepositionFromLatLng(currentLatLng: L.LatLng): void {
    if (!this._isDragging || !this.repositionDragStart || !this.repositionOrigBounds || !this.editingPontoonId) return;

    const layer = this.pontoonLayers.get(this.editingPontoonId);
    if (!layer) return;

    const dlat = currentLatLng.lat - this.repositionDragStart.lat;
    const dlng = currentLatLng.lng - this.repositionDragStart.lng;

    const newBounds = L.latLngBounds(
      [this.repositionOrigBounds.getSouthWest().lat + dlat, this.repositionOrigBounds.getSouthWest().lng + dlng],
      [this.repositionOrigBounds.getNorthEast().lat + dlat, this.repositionOrigBounds.getNorthEast().lng + dlng]
    );

    layer.setBounds(newBounds);
  }

  private finishRepositionDrag(): void {
    if (!this._isDragging || !this.editingPontoonId) return;

    const layer = this.pontoonLayers.get(this.editingPontoonId);
    if (layer) {
      const newBounds = layer.getBounds();
      const sw = newBounds.getSouthWest();
      const ne = newBounds.getNorthEast();

      this.pontoonService.updatePontoon(this.editingPontoonId, {
        southWestLat: sw.lat,
        southWestLng: sw.lng,
        northEastLat: ne.lat,
        northEastLng: ne.lng
      }).subscribe({
        next: () => {
          this.showToast('Position updated!', 'success');
          this.loadPontoons();
        },
        error: () => {
          this.showToast('Error updating position', 'error');
          this.loadPontoons();
        }
      });
    }

    this._isDragging = false;
    this.repositionDragStart = null;
    this.repositionOrigBounds = null;
    this.dragTouchId = null;

    if (this.map) {
      if (this.isTouchDevice) {
        this.map.dragging.disable();
      } else {
        this.map.dragging.enable();
      }
    }
  }

  private onContainerTouchStart(event: TouchEvent): void {
    if (!this.map || this.isDrawingMode || this._isDragging) return;

    if (event.touches.length >= 2) {
      this.map.dragging.enable();
    } else {
      this.map.dragging.disable();
    }
  }

  private onContainerTouchMove(event: TouchEvent): void {
    if (!this.map) return;

    if (this._isDragging) {
      const touch = this.getTrackedTouch(event);
      if (!touch) return;

      event.preventDefault();
      const latLng = this.touchToLatLng(touch);
      this.applyRepositionFromLatLng(latLng);
      return;
    }

    if (this.isDrawingMode) return;

    if (event.touches.length >= 2) {
      this.map.dragging.enable();
    } else {
      this.map.dragging.disable();
    }
  }

  private onContainerTouchEnd(event: TouchEvent): void {
    if (!this.map) return;

    if (this._isDragging) {
      const trackedTouchStillActive = this.dragTouchId !== null
        ? Array.from(event.touches).some(touch => touch.identifier === this.dragTouchId)
        : event.touches.length > 0;

      if (!trackedTouchStillActive) {
        event.preventDefault();
        this.finishRepositionDrag();
      }
      return;
    }

    if (this.isDrawingMode) return;

    if (event.touches.length >= 2) {
      this.map.dragging.enable();
    } else {
      this.map.dragging.disable();
    }
  }

  private getTrackedTouch(event: TouchEvent): Touch | null {
    if (this.dragTouchId === null) {
      return event.touches[0] ?? event.changedTouches[0] ?? null;
    }

    return Array.from(event.touches).find(touch => touch.identifier === this.dragTouchId)
      ?? Array.from(event.changedTouches).find(touch => touch.identifier === this.dragTouchId)
      ?? null;
  }

  private touchToLatLng(touch: Touch): L.LatLng {
    const containerRect = this.map!.getContainer().getBoundingClientRect();
    const containerPoint = L.point(
      touch.clientX - containerRect.left,
      touch.clientY - containerRect.top
    );

    return this.map!.containerPointToLatLng(containerPoint);
  }

  selectPontoon(pontoon: Pontoon): void {
    this.editingPontoonId = pontoon.id;
    this.editPontoonName = pontoon.name;
    this.editPontoonColor = pontoon.color || '#3388ff';
    this.isDrawingMode = false;

    if (this.map) {
      if (this.isTouchDevice) {
        this.map.dragging.disable();
        (this.map.getContainer() as HTMLElement).style.cursor = '';
      } else {
        this.map.dragging.enable();
        (this.map.getContainer() as HTMLElement).style.cursor = 'move';
      }
    }

    // Highlight selected pontoon
    this.pontoonLayers.forEach((layer, id) => {
      if (id === pontoon.id) {
        layer.setStyle({ weight: 4, dashArray: '5, 5' });
      } else {
        layer.setStyle({ weight: 2, dashArray: '' });
      }
    });
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
    this._isDragging = false;
    this.repositionDragStart = null;
    this.repositionOrigBounds = null;
    this.dragTouchId = null;

    if (this.map) {
      if (this.isTouchDevice) {
        this.map.dragging.disable();
      } else {
        this.map.dragging.enable();
      }
      (this.map.getContainer() as HTMLElement).style.cursor = '';
    }

    // Remove highlight
    this.pontoonLayers.forEach(layer => {
      layer.setStyle({ weight: 2, dashArray: '' });
    });
  }

  goBack(): void {
    this.router.navigate(['/profile']);
  }

  private showToast(msg: string, type: 'success' | 'error'): void {
    this.showMessage = msg;
    this.messageType = type;
    setTimeout(() => this.showMessage = '', 3000);
  }
}
