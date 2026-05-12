import { Component, ChangeDetectorRef, OnDestroy, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { switchMap } from 'rxjs/operators';
import * as L from 'leaflet';
import { AuthService } from '../../services/auth.service';
import { GeocodingService } from '../../services/geocoding.service';
import { ManagerApplicationService } from '../../services/manager-application.service';
import {
  ManagerApplication,
  UpsertManagerApplication
} from '../../models/manager-application.model';

export type WizardStep = 'lake' | 'location' | 'motivation' | 'review';

interface WizardForm {
  lakeName: string;
  description: string;
  proposedPricePerHour: number;
  fishSpeciesInput: string;
  fishSpecies: string[];
  contactPhone: string;
  latitude: number | null;
  longitude: number | null;
  locationLabel: string;
  motivation: string;
  administrationBasis: string;
}

@Component({
  selector: 'app-manager-application',
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './manager-application.html',
  styleUrl: './manager-application.css',
  encapsulation: ViewEncapsulation.None
})
export class ManagerApplicationPage implements OnInit, OnDestroy {
  private map: L.Map | null = null;
  private marker: L.Marker | null = null;
  private readonly mapClickHandler = (e: L.LeafletMouseEvent) => this.onMapClick(e);
  private mapTimer: number | null = null;

  readonly steps: WizardStep[] = ['lake', 'location', 'motivation', 'review'];
  readonly stepLabels: Record<WizardStep, string> = {
    lake: 'Lake Details',
    location: 'Location',
    motivation: 'Motivation',
    review: 'Review & Submit'
  };

  currentStep: WizardStep = 'lake';
  loading = true;
  saving = false;
  resolvingLocation = false;
  stepError = '';
  globalError = '';
  successMessage = '';

  latestApplication: ManagerApplication | null = null;
  allApplications: ManagerApplication[] = [];

  form: WizardForm = this.emptyForm();

  constructor(
    private authService: AuthService,
    private geocodingService: GeocodingService,
    private managerApplicationService: ManagerApplicationService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }
    if (!this.authService.isUser()) {
      this.router.navigate(['/home']);
      return;
    }
    this.loadApplications();
  }

  ngOnDestroy(): void {
    this.clearMapTimer();
    this.destroyMap();
  }

  // ── getters ────────────────────────────────────────────────────────────────

  get stepIndex(): number { return this.steps.indexOf(this.currentStep); }
  get isFirstStep(): boolean { return this.stepIndex === 0; }
  get isLastStep(): boolean { return this.stepIndex === this.steps.length - 1; }

  get isPending(): boolean { return this.latestApplication?.status === 'Pending'; }
  get isRejected(): boolean { return this.latestApplication?.status === 'Rejected'; }
  get isApproved(): boolean { return this.latestApplication?.status === 'Approved'; }
  get isSubmittedReadOnly(): boolean { return this.isPending || this.isApproved; }
  get isReadOnly(): boolean { return this.isSubmittedReadOnly || this.saving; }

  get pageTitle(): string {
    return this.authService.isManager() ? 'Propose a New Lake' : 'Apply for Manager';
  }

  get submitLabel(): string {
    if (this.saving) return 'Saving…';
    return this.isRejected ? 'Save Changes' : 'Submit Application';
  }

  get resubmitLabel(): string {
    return this.saving ? 'Resubmitting…' : 'Resubmit';
  }

  // ── navigation ─────────────────────────────────────────────────────────────

  goToStep(step: WizardStep): void {
    if (this.saving) return;

    if (this.isSubmittedReadOnly) {
      this.stepError = '';
      this.currentStep = step;
      if (step === 'location') this.queueMapInit();
      return;
    }

    const target = this.steps.indexOf(step);
    const current = this.stepIndex;
    if (target > current) {
      for (let i = current; i < target; i++) {
        if (!this.validateStep(this.steps[i])) return;
      }
    }
    this.stepError = '';
    this.currentStep = step;
    if (step === 'location') this.queueMapInit();
  }

  nextStep(): void {
    if (this.saving || this.isLastStep) return;

    if (this.isSubmittedReadOnly) {
      this.stepError = '';
      const next = this.steps[this.stepIndex + 1];
      this.currentStep = next;
      if (next === 'location') this.queueMapInit();
      return;
    }

    if (!this.validateStep(this.currentStep)) return;
    this.stepError = '';
    const next = this.steps[this.stepIndex + 1];
    this.currentStep = next;
    if (next === 'location') this.queueMapInit();
  }

  prevStep(): void {
    if (this.saving || this.isFirstStep) return;
    this.stepError = '';
    this.currentStep = this.steps[this.stepIndex - 1];
    if (this.currentStep === 'location') this.queueMapInit();
  }

  // ── actions ────────────────────────────────────────────────────────────────

  loadApplications(): void {
    this.destroyMap();
    this.loading = true;
    this.managerApplicationService.getMine().subscribe({
      next: (apps) => {
        this.allApplications = apps;
        this.latestApplication = apps[0] ?? null;
        this.syncFormFromApplication();
        this.loading = false;
        this.cdr.detectChanges();
      },
    error: () => {
        this.globalError = 'Could not load your applications.';
        this.loading = false;
      }
    });
  }

  addSpecies(): void {
    if (this.isReadOnly) return;
    const name = this.form.fishSpeciesInput.trim();
    if (!name) return;
    if (!this.form.fishSpecies.some(s => s.toLowerCase() === name.toLowerCase())) {
      this.form.fishSpecies = [...this.form.fishSpecies, name];
    }
    this.form.fishSpeciesInput = '';
  }

  removeSpecies(s: string): void {
    if (this.isReadOnly) return;
    this.form.fishSpecies = this.form.fishSpecies.filter(x => x !== s);
  }

  onSpeciesEnter(e: Event): void { e.preventDefault(); this.addSpecies(); }

  onPhoneChange(v: string): void {
    this.form.contactPhone = v.replace(/\D/g, '').slice(0, 10);
  }

  submit(): void {
    if (this.isReadOnly) return;
    if (!this.validateAll()) return;

    const payload = this.buildPayload();
    if (!payload) return;

    this.saving = true;
    this.globalError = '';

    const req$ = this.isRejected && this.latestApplication
      ? this.managerApplicationService.update(this.latestApplication.id, payload)
      : this.managerApplicationService.create(payload);

    req$.subscribe({
      next: () => {
        this.successMessage = this.isRejected
          ? 'Changes saved. You can resubmit when ready.'
          : 'Application submitted! An admin will review it soon.';
        this.saving = false;
        this.loadApplications();
      },
      error: (res) => {
        this.globalError = res.error?.message ?? 'Could not save the application.';
        this.saving = false;
      }
    });
  }

  saveAndResubmit(): void {
    if (!this.latestApplication || !this.isRejected) return;
    if (!this.validateAll()) return;

    const payload = this.buildPayload();
    if (!payload) return;

    this.saving = true;
    this.globalError = '';

    this.managerApplicationService.update(this.latestApplication.id, payload).pipe(
      switchMap(updated => this.managerApplicationService.resubmit(updated.id))
    ).subscribe({
      next: () => {
        this.successMessage = 'Application resubmitted to admin.';
        this.saving = false;
        this.loadApplications();
      },
      error: (res) => {
        this.globalError = res.error?.message ?? 'Could not resubmit.';
        this.saving = false;
      }
    });
  }

  startNew(): void {
    if (this.isPending) return;
    this.latestApplication = null;
    this.form = this.emptyForm();
    this.currentStep = 'lake';
    this.stepError = '';
    this.globalError = '';
    this.successMessage = '';
    this.destroyMap();
  }

  formatDate(d?: string): string {
    if (!d) return '–';
    const parsed = new Date(d);
    if (isNaN(parsed.getTime())) return '–';
    return new Intl.DateTimeFormat('en-US', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(parsed);
  }

  parsedSpecies(raw?: string): string[] {
    if (!raw) return [];
    try {
      const p = JSON.parse(raw) as string[];
      return Array.isArray(p) ? p.filter(Boolean) : [];
    } catch {
      return raw.split(/[,\n]/).map(s => s.trim()).filter(Boolean);
    }
  }

  // ── map ────────────────────────────────────────────────────────────────────

  private queueMapInit(): void {
    this.clearMapTimer();
    this.mapTimer = window.setTimeout(() => {
      this.mapTimer = null;
      this.initMap();
    }, 60);
  }

  private initMap(): void {
    const el = document.getElementById('wizard-map');
    if (!el) return;

    this.destroyMap();

    const hasPin = this.form.latitude != null && this.form.longitude != null;
    const center: [number, number] = hasPin
      ? [this.form.latitude!, this.form.longitude!]
      : [45.9432, 24.9668];
    const zoom = hasPin ? 12 : 6;

    this.map = L.map(el, {
      center,
      zoom,
      zoomControl: true,
      maxZoom: 19
    });

    L.tileLayer('https://{s}.google.com/vt/lyrs=y&hl=en&x={x}&y={y}&z={z}', {
      attribution: '&copy; <a href="https://maps.google.com">Google Maps</a>',
      maxZoom: 20,
      subdomains: ['mt0', 'mt1', 'mt2', 'mt3']
    }).addTo(this.map);

    this.map.on('click', this.mapClickHandler);

    if (hasPin) {
      this.syncMarker();
    } else if (navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        ({ coords }) => {
          if (this.map && this.form.latitude == null) {
            this.map.setView([coords.latitude, coords.longitude], 9, { animate: false });
          }
        },
        () => {},
        { timeout: 6000 }
      );
    }

    window.setTimeout(() => this.map?.invalidateSize(), 120);
  }

  private onMapClick(e: L.LeafletMouseEvent): void {
    if (this.isReadOnly) return;
    this.form.latitude = +e.latlng.lat.toFixed(6);
    this.form.longitude = +e.latlng.lng.toFixed(6);
    this.syncMarker();
    this.resolveLabel(e.latlng.lat, e.latlng.lng);
  }

  private syncMarker(): void {
    if (!this.map) return;
    if (this.form.latitude == null || this.form.longitude == null) {
      this.removeMarker();
      return;
    }
    const ll: [number, number] = [this.form.latitude, this.form.longitude];
    if (!this.marker) {
      delete (L.Icon.Default.prototype as any)._getIconUrl;
      L.Icon.Default.mergeOptions({
        iconUrl: 'assets/marker-icon.png',
        iconRetinaUrl: 'assets/marker-icon-2x.png',
        shadowUrl: 'assets/marker-shadow.png'
      });
      this.marker = L.marker(ll).addTo(this.map);
    } else {
      this.marker.setLatLng(ll);
    }
    this.map.setView(ll, Math.max(this.map.getZoom(), 10), { animate: false });
  }

  private removeMarker(): void {
    if (this.marker && this.map) { this.map.removeLayer(this.marker); this.marker = null; }
  }

  private resolveLabel(lat: number, lng: number): void {
    this.resolvingLocation = true;
    this.form.locationLabel = 'Resolving address…';
    this.geocodingService.reverseGeocode(lat, lng, 'en').subscribe({
      next: (res) => {
        const a = res.address ?? {};
        this.form.locationLabel = [
          a['road'], a['suburb'],
          a['city'] ?? a['town'] ?? a['village'],
          a['county']
        ].filter(Boolean).join(', ') || res.displayName;
        this.resolvingLocation = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.form.locationLabel = `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
        this.resolvingLocation = false;
        this.cdr.detectChanges();
      }
    });
  }

  private destroyMap(): void {
    if (!this.map) return;
    this.map.off('click', this.mapClickHandler);
    this.map.remove();
    this.map = null;
    this.marker = null;
  }

  private clearMapTimer(): void {
    if (this.mapTimer !== null) { window.clearTimeout(this.mapTimer); this.mapTimer = null; }
  }

  // ── helpers ────────────────────────────────────────────────────────────────

  private emptyForm(): WizardForm {
    return {
      lakeName: '', description: '', proposedPricePerHour: 0,
      fishSpeciesInput: '', fishSpecies: [], contactPhone: '',
      latitude: null, longitude: null, locationLabel: '',
      motivation: '', administrationBasis: ''
    };
  }

  private syncFormFromApplication(): void {
    const app = this.latestApplication;
    if (!app) { this.form = this.emptyForm(); return; }
    this.form = {
      lakeName: app.lakeName,
      description: app.description ?? '',
      proposedPricePerHour: app.proposedPricePerHour,
      fishSpeciesInput: '',
      fishSpecies: this.parsedSpecies(app.fishSpecies),
      contactPhone: app.contactPhone.slice(0, 10),
      latitude: app.latitude,
      longitude: app.longitude,
      locationLabel: app.locationLabel ?? '',
      motivation: app.motivation,
      administrationBasis: app.administrationBasis
    };
  }

  private validateStep(step: WizardStep): boolean {
    this.stepError = '';
    if (step === 'lake') {
      if (!this.form.lakeName.trim()) { this.stepError = 'Enter the lake name.'; return false; }
      if (!this.form.contactPhone.trim()) { this.stepError = 'Enter a contact phone number.'; return false; }
      if (this.form.contactPhone.length > 10) { this.stepError = 'Phone number can have at most 10 digits.'; return false; }
    }
    if (step === 'location') {
      if (this.form.latitude == null || this.form.longitude == null) {
        this.stepError = 'Select the lake position on the map.'; return false;
      }
    }
    if (step === 'motivation') {
      if (!this.form.motivation.trim()) { this.stepError = 'Add your motivation / relevant experience.'; return false; }
      if (!this.form.administrationBasis.trim()) { this.stepError = 'Specify the administrative basis.'; return false; }
    }
    return true;
  }

  private validateAll(): boolean {
    for (const step of ['lake', 'location', 'motivation'] as WizardStep[]) {
      if (!this.validateStep(step)) {
        this.globalError = this.stepError;
        this.stepError = '';
        return false;
      }
    }
    return true;
  }

  private buildPayload(): UpsertManagerApplication | null {
    if (this.form.latitude == null || this.form.longitude == null) return null;
    const species = this.form.fishSpecies;
    return {
      lakeName: this.form.lakeName.trim(),
      description: this.norm(this.form.description),
      latitude: this.form.latitude,
      longitude: this.form.longitude,
      locationLabel: this.norm(this.form.locationLabel),
      proposedPricePerHour: this.form.proposedPricePerHour || 0,
      fishSpecies: species.length > 0 ? JSON.stringify(species) : undefined,
      contactPhone: this.form.contactPhone.trim(),
      motivation: this.form.motivation.trim(),
      administrationBasis: this.form.administrationBasis.trim()
    };
  }

  private norm(v: string): string | undefined {
    const t = v.trim(); return t.length > 0 ? t : undefined;
  }
}
