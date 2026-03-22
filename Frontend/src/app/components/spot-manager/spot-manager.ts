import { Component, OnInit, OnDestroy, ViewEncapsulation } from '@angular/core';
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
  styleUrl: './spot-manager.css',
  encapsulation: ViewEncapsulation.None
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

  private map: L.Map | null = null;
  private pontoonLayers: Map<number, L.Polygon | L.Rectangle> = new Map();
  private drawingPolygon: L.Polygon | null = null;
  private drawingMarkers: L.CircleMarker[] = [];
  private editVertexMarkers: L.CircleMarker[] = [];

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
        
        const userId = this.authService.getUserId();
        if (!this.authService.isAdmin() && spot.managerId !== userId && spot.userId !== userId) {
          this.router.navigate(['/home']);
          return;
        }
        
        this.editDescription = spot.description || '';
        this.fishSpeciesList = spot.fishSpecies ? JSON.parse(spot.fishSpecies) : [];

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
      layer.on('click', () => this.selectPontoon(pontoon));
      this.pontoonLayers.set(pontoon.id, layer);
    });
  }

  // ---- Drawing Mode ----

  toggleDrawingMode(): void {
    this.isDrawingMode = !this.isDrawingMode;
    this.editingPontoonId = null;
    this.clearEditVertexMarkers();

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
    if (!this.isDrawingMode || !this.map) return;

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

    this.map!.getContainer().addEventListener('touchmove', (te: TouchEvent) => {
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
    }, { passive: false });

    this.map!.getContainer().addEventListener('touchend', () => {
      if (!dragging) return;
      dragging = false;
      this.map!.dragging.enable();
      this.saveVertexPositions(pontoon);
    });
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
    } as any).subscribe({
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
    } as any).subscribe({
      next: () => {
        this.spot!.fishSpecies = json;
        this.showToast('Fish species updated!', 'success');
      },
      error: () => this.showToast('Error saving fish species', 'error')
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
