import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';

export interface ReverseGeocodeResult {
  displayName: string;
  address?: Record<string, string>;
}

@Injectable({ providedIn: 'root' })
export class GeocodingService {
  private readonly nominatimUrl = 'https://nominatim.openstreetmap.org/reverse';

  constructor(private http: HttpClient) {}

  reverseGeocode(lat: number, lng: number, lang = 'ro'): Observable<ReverseGeocodeResult> {
    return this.http
      .get<{ display_name?: string; address?: Record<string, string> }>(
        `${this.nominatimUrl}?lat=${lat}&lon=${lng}&format=json&accept-language=${lang}`
      )
      .pipe(
        map(data => ({
          displayName: data.display_name ?? `${lat.toFixed(5)}, ${lng.toFixed(5)}`,
          address: data.address
        }))
      );
  }
}
