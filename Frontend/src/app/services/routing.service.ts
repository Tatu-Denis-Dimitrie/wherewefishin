import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';

export interface RouteResult {
  geometry: GeoJSON.Geometry;
  distanceKm: string;
  durationMin: number;
  durationText: string;
}

@Injectable({ providedIn: 'root' })
export class RoutingService {
  private readonly osrmUrl = 'https://router.project-osrm.org/route/v1/driving';

  constructor(private http: HttpClient) {}

  getRoute(originLng: number, originLat: number, destLng: number, destLat: number): Observable<RouteResult> {
    const url = `${this.osrmUrl}/${originLng},${originLat};${destLng},${destLat}?overview=full&geometries=geojson`;
    return this.http.get<any>(url).pipe(
      map(data => {
        if (data.code !== 'Ok' || !data.routes?.length) {
          throw new Error('Could not calculate route');
        }
        const route = data.routes[0];
        const durationMin = Math.round(route.duration / 60);
        return {
          geometry: route.geometry,
          distanceKm: (route.distance / 1000).toFixed(1),
          durationMin,
          durationText: durationMin >= 60
            ? `${Math.floor(durationMin / 60)}h ${durationMin % 60}min`
            : `${durationMin} min`
        };
      })
    );
  }
}
