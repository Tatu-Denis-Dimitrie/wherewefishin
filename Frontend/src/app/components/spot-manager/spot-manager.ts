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
      dragging: true,
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

      this.pontoonLayers.set(pontoon.id, rect);
    });
  }

  toggleDrawingMode(): void {
    this.isDrawingMode = !this.isDrawingMode;
    this.editingPontoonId = null;
    this._isDragging = false;
    this.repositionDragStart = null;

    if (this.map) {
      if (this.isDrawingMode) {
        this.map.dragging.disable();
        (this.map.getContainer() as HTMLElement).style.cursor = 'crosshair';
      } else {
        this.map.dragging.enable();
        (this.map.getContainer() as HTMLElement).style.cursor = '';
      }
    }
  }

  private onMapMouseDown(e: L.LeafletMouseEvent): void {
    if (!this.isDrawingMode || !this.map) return;

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
    if (this._isDragging && this.repositionDragStart && this.editingPontoonId) {
      const layer = this.pontoonLayers.get(this.editingPontoonId);
      if (!layer || !this.repositionOrigBounds) return;

      const dlat = e.latlng.lat - this.repositionDragStart.lat;
      const dlng = e.latlng.lng - this.repositionDragStart.lng;

      const newBounds = L.latLngBounds(
        [this.repositionOrigBounds.getSouthWest().lat + dlat, this.repositionOrigBounds.getSouthWest().lng + dlng],
        [this.repositionOrigBounds.getNorthEast().lat + dlat, this.repositionOrigBounds.getNorthEast().lng + dlng]
      );
      layer.setBounds(newBounds);
      return;
    }

    // Handle drawing drag
    if (!this.isDrawingMode || !this.drawingRect || !this.startLatLng) return;
    this.drawingRect.setBounds(L.latLngBounds(this.startLatLng, e.latlng));
  }

  private onMapMouseUp(): void {
    // Handle repositioning end
    if (this._isDragging && this.repositionDragStart && this.editingPontoonId) {
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
            this.showToast('Poziție actualizată!', 'success');
            this.loadPontoons();
          },
          error: () => {
            this.showToast('Eroare la actualizarea poziției', 'error');
            this.loadPontoons(); // Reload to revert visual
          }
        });
      }
      this._isDragging = false;
      this.repositionDragStart = null;
      this.repositionOrigBounds = null;
      if (this.map) this.map.dragging.enable();
      return;
    }

    // Handle drawing end
    if (!this.isDrawingMode || !this.drawingRect || !this.startLatLng || !this.spot) return;

    const bounds = this.drawingRect.getBounds();
    const sw = bounds.getSouthWest();
    const ne = bounds.getNorthEast();

    // Check if it's a valid rectangle (not just a click)
    if (Math.abs(sw.lat - ne.lat) < 0.00001 || Math.abs(sw.lng - ne.lng) < 0.00001) {
      this.drawingRect.remove();
      this.drawingRect = null;
      this.startLatLng = null;
      return;
    }

    // Save the pontoon
    const pontoonData: CreatePontoon = {
      fishingSpotId: this.spot.id,
      name: this.newPontoonName || `Ponton ${this.pontoons.length + 1}`,
      southWestLat: sw.lat,
      southWestLng: sw.lng,
      northEastLat: ne.lat,
      northEastLng: ne.lng,
      color: this.newPontoonColor
    };

    this.pontoonService.createPontoon(pontoonData).subscribe({
      next: () => {
        this.showToast('Ponton adăugat cu succes!', 'success');
        this.loadPontoons();
        this.newPontoonName = '';
        this.toggleDrawingMode();
      },
      error: () => {
        this.showToast('Eroare la adăugarea pontonului', 'error');
      }
    });

    this.drawingRect.remove();
    this.drawingRect = null;
    this.startLatLng = null;
  }

  // ---------- Repositioning ----------

  private onPontoonDragStart(e: L.LeafletMouseEvent, pontoonId: number): void {
    if (this.editingPontoonId !== pontoonId || this.isDrawingMode) return;

    L.DomEvent.stopPropagation(e);
    const layer = this.pontoonLayers.get(pontoonId);
    if (!layer) return;

    this._isDragging = true;
    if (this.map) this.map.dragging.disable();

    this.repositionDragStart = e.latlng;
    this.repositionOrigBounds = L.latLngBounds(
      layer.getBounds().getSouthWest(),
      layer.getBounds().getNorthEast()
    );
  }

  selectPontoon(pontoon: Pontoon): void {
    this.editingPontoonId = pontoon.id;
    this.editPontoonName = pontoon.name;
    this.editPontoonColor = pontoon.color || '#3388ff';
    this.isDrawingMode = false;

    if (this.map) {
      this.map.dragging.enable();
      (this.map.getContainer() as HTMLElement).style.cursor = 'move';
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
        this.showToast('Ponton actualizat!', 'success');
        this.loadPontoons();
        this.cancelEdit();
      },
      error: () => {
        this.showToast('Eroare la actualizarea pontonului', 'error');
      }
    });
  }

  deletePontoon(): void {
    if (!this.editingPontoonId) return;
    if (!confirm('Sigur dorești să ștergi acest ponton?')) return;

    this.pontoonService.deletePontoon(this.editingPontoonId).subscribe({
      next: () => {
        this.showToast('Ponton șters!', 'success');
        this.loadPontoons();
        this.cancelEdit();
      },
      error: () => {
        this.showToast('Eroare la ștergerea pontonului', 'error');
      }
    });
  }

  cancelEdit(): void {
    this.editingPontoonId = null;
    this.editPontoonName = '';
    this.editPontoonColor = '';
    this._isDragging = false;
    this.repositionDragStart = null;

    if (this.map) {
      this.map.dragging.enable();
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
