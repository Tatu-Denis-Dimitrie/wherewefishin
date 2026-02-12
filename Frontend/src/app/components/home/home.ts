import { Component, OnInit, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import * as L from 'leaflet';
import 'leaflet-draw';

const STORAGE_KEY = 'wherewefishin_shapes';

@Component({
  selector: 'app-home',
  imports: [CommonModule],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class Home implements OnInit, AfterViewInit, OnDestroy {
  private map!: L.Map;
  private drawnItems!: L.FeatureGroup;
  showSaveMessage = false;
  isDeleteMode = false;

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
    }
  }

  ngAfterViewInit(): void {
    this.initMap();
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.remove();
    }
  }

  private initMap(): void {
    const iconRetinaUrl = 'assets/marker-icon-2x.png';
    const iconUrl = 'assets/marker-icon.png';
    const shadowUrl = 'assets/marker-shadow.png';
    const iconDefault = L.icon({
      iconRetinaUrl,
      iconUrl,
      shadowUrl,
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

    this.drawnItems = new L.FeatureGroup();
    this.map.addLayer(this.drawnItems);

    const drawControl = new L.Control.Draw({
      edit: {
        featureGroup: this.drawnItems,
        remove: false,
        edit: false
      },
      draw: {
        rectangle: {
          shapeOptions: {
            color: '#06b6d4',
            fillColor: '#06b6d4',
            fillOpacity: 0.3
          }
        },
        polygon: false,
        circle: false,
        marker: false,
        polyline: false,
        circlemarker: false
      }
    });
    this.map.addControl(drawControl);

    this.loadSavedShapes();

    this.map.on(L.Draw.Event.CREATED, (event: any) => {
      const layer = event.layer;
      this.drawnItems.addLayer(layer);
      
      layer.bindPopup('Fishing Pontoon / Area').openPopup();
      
      if (event.layerType === 'rectangle') {
        layer.on('click', () => {
          layer.editing.enable();
        });
      }
      
      this.saveShapes();
    });

    this.map.on(L.Draw.Event.EDITED, (event: any) => {
      this.saveShapes();
    });

    this.map.on(L.Draw.Event.DELETED, (event: any) => {
      this.saveShapes();
      this.showSaveMessage = true;
      setTimeout(() => {
        this.showSaveMessage = false;
      }, 2000);
    });
  }

  private saveShapes(): void {
    const shapes = this.getDrawnShapes();
    localStorage.setItem(STORAGE_KEY, JSON.stringify(shapes));
  }

  private loadSavedShapes(): void {
    const savedData = localStorage.getItem(STORAGE_KEY);
    if (savedData) {
      try {
        const shapes = JSON.parse(savedData);
        shapes.forEach((geoJson: any) => {
          const layer = L.geoJSON(geoJson, {
            style: {
              color: '#06b6d4',
              fillColor: '#06b6d4',
              fillOpacity: 0.3
            }
          });
          layer.eachLayer((l: any) => {
            l.bindPopup('Fishing Pontoon / Area');
            this.drawnItems.addLayer(l);
            
            l.on('click', () => {
              l.editing.enable();
            });
          });
        });
      } catch (e) {
        console.error('Error loading saved shapes:', e);
      }
    }
  }

  toggleDeleteMode(): void {
    this.isDeleteMode = !this.isDeleteMode;
    
    if (this.isDeleteMode) {
      this.drawnItems.eachLayer((layer: any) => {
        layer.on('click', this.deleteLayer.bind(this, layer));
      });
    } else {
      this.drawnItems.eachLayer((layer: any) => {
        layer.off('click');
        layer.on('click', () => {
          if (!this.isDeleteMode) {
            layer.editing.enable();
          }
        });
      });
    }
  }

  private deleteLayer(layer: any): void {
    if (this.isDeleteMode) {
      this.drawnItems.removeLayer(layer);
      this.saveShapes();
      this.showSaveMessage = true;
      setTimeout(() => {
        this.showSaveMessage = false;
      }, 2000);
    }
  }

  saveShapesManually(): void {
    this.drawnItems.eachLayer((layer: any) => {
      if (layer.editing) {
        layer.editing.disable();
      }
    });
    
    if ((this.map as any)._toolbars) {
      (this.map as any)._toolbars.forEach((toolbar: any) => {
        if (toolbar._modes) {
          Object.keys(toolbar._modes).forEach((key) => {
            const handler = toolbar._modes[key].handler;
            if (handler.enabled()) {
              handler.disable();
            }
          });
        }
      });
    }
    
    this.saveShapes();
    this.showSaveMessage = true;
    setTimeout(() => {
      this.showSaveMessage = false;
    }, 2000);
  }

  logout(): void {
    this.authService.logout();
  }

  goToProfile(): void {
    this.router.navigate(['/profile']);
  }

  goToFishRecognition(): void {
    this.router.navigate(['/fish-recognition']);
  }

  getDrawnShapes(): any[] {
    const shapes: any[] = [];
    this.drawnItems.eachLayer((layer: any) => {
      shapes.push(layer.toGeoJSON());
    });
    return shapes;
  }
}
