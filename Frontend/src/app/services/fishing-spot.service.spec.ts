import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { FishingSpotService } from './fishing-spot.service';

describe('FishingSpotService', () => {
  let service: FishingSpotService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        FishingSpotService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(FishingSpotService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('uses a new cache-busting revision after cache is cleared', () => {
    service.getAll().subscribe();

    const initialRequest = httpMock.expectOne(request =>
      request.method === 'GET' &&
      request.url.endsWith('/api/fishingspots') &&
      request.params.get('v') === '0' &&
      request.params.get('ngsw-bypass') === 'true');
    initialRequest.flush([]);

    service.clearCache();
    service.getAll().subscribe();

    const refreshedRequest = httpMock.expectOne(request =>
      request.method === 'GET' &&
      request.url.endsWith('/api/fishingspots') &&
      request.params.get('v') === '1' &&
      request.params.get('ngsw-bypass') === 'true');
    refreshedRequest.flush([]);
  });

  it('invalidates cached listings after create succeeds', () => {
    service.create({
      name: 'New Lake',
      latitude: 45.1,
      longitude: 25.2,
      pricePerHour: 20
    }).subscribe();

    const createRequest = httpMock.expectOne(request =>
      request.method === 'POST' && request.url.endsWith('/api/fishingspots'));
    createRequest.flush({ id: 1, name: 'New Lake' });

    service.getAll().subscribe();

    const refreshedRequest = httpMock.expectOne(request =>
      request.method === 'GET' &&
      request.url.endsWith('/api/fishingspots') &&
      request.params.get('v') === '1' &&
      request.params.get('ngsw-bypass') === 'true');
    refreshedRequest.flush([]);
  });
});