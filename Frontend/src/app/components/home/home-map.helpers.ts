import * as L from 'leaflet';
import { renderAppIconSvg } from '../../shared/icons/app-icon.registry';

export interface HomePopupSpotData {
  name: string;
  description?: string | null;
  latitude: number;
  longitude: number;
  pricePerHour: number;
  parsedFishSpecies: string[];
}

export interface HomePopupOptions {
  primaryActionLabel?: string;
}

export function createHomeSpotIcon(fillColor = '#4a7c30'): L.DivIcon {
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

export function buildHomeSpotPopupContent(spot: HomePopupSpotData, options?: HomePopupOptions): string {
  const primaryActionLabel = options?.primaryActionLabel ?? 'Book Pontoon';
  const priceHtml = spot.pricePerHour > 0
    ? `<div style="display:flex;align-items:center;gap:6px;margin:6px 0 2px">
         <span style="background:#4a7c3022;color:#4a7c30;font-size:11px;font-weight:700;padding:2px 8px;border-radius:12px;border:1px solid #4a7c3044">${spot.pricePerHour} RON / h</span>
       </div>`
    : '';
  const fishHtml = spot.parsedFishSpecies.length > 0
    ? `<div style="color:#93c5fd;font-size:11px;margin-bottom:6px;line-height:1.35">Fish: ${spot.parsedFishSpecies.join(', ')}</div>`
    : '';

  const pontoonSvg = renderAppIconSvg('pontoons', {
    size: 13,
    strokeWidth: 2.5,
    strokeLinecap: 'round',
    strokeLinejoin: 'round',
    style: 'vertical-align:middle;margin-right:5px;margin-bottom:1px'
  });
  const navSvg = renderAppIconSvg('route', {
    size: 13,
    style: 'vertical-align:middle;margin-right:5px;margin-bottom:1px'
  });

  let html = `<div style="min-width:195px;font-family:inherit">`;
  html += `<div style="font-size:15px;font-weight:700;color:#1e293b;margin-bottom:3px">${spot.name}</div>`;
  if (spot.description) {
    html += `<div style="color:#64748b;font-size:12px;margin-bottom:4px;line-height:1.4">${spot.description}</div>`;
  }
  html += priceHtml;
  html += fishHtml;
  html += `<div style="color:#94a3b8;font-size:10px;margin-bottom:10px">${spot.latitude.toFixed(5)}, ${spot.longitude.toFixed(5)}</div>`;
  html += `<button class="popup-book-btn" style="display:flex;align-items:center;justify-content:center;padding:8px 14px;border-radius:8px;font-size:12px;font-weight:600;cursor:pointer;width:100%;background:#4a7c30;color:#fff;border:none;transition:filter .15s;">${pontoonSvg}${primaryActionLabel}</button>`;
  html += `<button class="popup-route-btn" style="display:flex;align-items:center;justify-content:center;padding:7px 14px;border-radius:8px;font-size:12px;font-weight:600;cursor:pointer;width:100%;margin-top:6px;background:#1e3a5f;color:#60a5fa;border:1px solid #2563eb55;transition:all .15s">${navSvg}Route on map</button>`;
  html += `</div>`;
  return html;
}

export function createHomeUserLocationIcon(): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `
      <div style="position:relative;width:18px;height:18px;overflow:visible">
        <div style="
          position:absolute;width:44px;height:44px;
          background:rgba(66,133,244,0.22);border-radius:50%;
          top:50%;left:50%;
          transform:translate(-50%,-50%) scale(0.3);
          animation:userLocPulse 2.2s ease-out infinite;
          pointer-events:none;
        "></div>
        <div style="
          position:absolute;width:18px;height:18px;
          background:#4285f4;border:3px solid #fff;border-radius:50%;
          box-shadow:0 2px 10px rgba(66,133,244,0.6);
          top:0;left:0;cursor:pointer;
          transition:transform 0.15s ease;
        "></div>
      </div>`,
    iconSize: [18, 18],
    iconAnchor: [9, 9],
    popupAnchor: [0, -14]
  });
}

export function buildPendingUserLocationPopup(accuracyText: string): string {
  return `<div style="font-family:inherit;min-width:160px">
    <b style="font-size:13px;color:#1e293b">Your Location</b><br>
    <span style="font-size:11px;color:#64748b">GPS Accuracy: <b>${accuracyText}</b></span><br>
    <span style="font-size:11px;color:#94a3b8">Resolving address...</span>
  </div>`;
}

export function buildResolvedUserLocationPopup(place: string, accuracyText: string): string {
  return `<div style="font-family:inherit;min-width:160px">
    <b style="font-size:13px;color:#1e293b">Your Location</b><br>
    <span style="font-size:12px;color:#334155">${place}</span><br>
    <span style="font-size:11px;color:#64748b;margin-top:3px;display:block">GPS Accuracy: <b>${accuracyText}</b></span>
  </div>`;
}

export function buildFallbackUserLocationPopup(accuracyText: string): string {
  return `<div style="font-family:inherit">
    <b style="font-size:13px;color:#1e293b">Your Location</b><br>
    <span style="font-size:11px;color:#64748b">GPS Accuracy: <b>${accuracyText}</b></span>
  </div>`;
}