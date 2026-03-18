import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface Review {
  id: number;
  fishingSpotId: number;
  userId: number;
  username: string;
  userProfilePictureUrl?: string;
  rating: number;
  comment?: string;
  createdAt: Date;
}

export interface CreateReview {
  fishingSpotId: number;
  rating: number;
  comment?: string;
}

export interface UpdateReview {
  rating?: number;
  comment?: string;
}

export interface ReviewStats {
  averageRating: number | null;
  totalReviews: number;
}

@Injectable({
  providedIn: 'root'
})
export class ReviewService {
  private apiUrl = `${environment.apiBaseUrl}/api/reviews`;
  private reviewsCache = new Map<number, Observable<Review[]>>();
  private statsCache = new Map<number, Observable<ReviewStats>>();

  constructor(private http: HttpClient) {}

  getSpotReviews(fishingSpotId: number): Observable<Review[]> {
    if (!this.reviewsCache.has(fishingSpotId)) {
      this.reviewsCache.set(fishingSpotId,
        this.http.get<Review[]>(`${this.apiUrl}/spot/${fishingSpotId}`).pipe(
          shareReplay({ bufferSize: 1, refCount: true })
        )
      );
    }
    return this.reviewsCache.get(fishingSpotId)!;
  }

  getAverageRating(fishingSpotId: number): Observable<ReviewStats> {
    if (!this.statsCache.has(fishingSpotId)) {
      this.statsCache.set(fishingSpotId,
        this.http.get<ReviewStats>(`${this.apiUrl}/spot/${fishingSpotId}/average`).pipe(
          shareReplay({ bufferSize: 1, refCount: true })
        )
      );
    }
    return this.statsCache.get(fishingSpotId)!;
  }

  private clearSpotCache(fishingSpotId: number): void {
    this.reviewsCache.delete(fishingSpotId);
    this.statsCache.delete(fishingSpotId);
  }

  getReview(id: number): Observable<Review> {
    return this.http.get<Review>(`${this.apiUrl}/${id}`);
  }

  createReview(review: CreateReview): Observable<Review> {
    return this.http.post<Review>(this.apiUrl, review).pipe(
      tap(r => this.clearSpotCache(r.fishingSpotId))
    );
  }

  updateReview(id: number, review: UpdateReview, fishingSpotId: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, review).pipe(
      tap(() => this.clearSpotCache(fishingSpotId))
    );
  }

  deleteReview(id: number, fishingSpotId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.clearSpotCache(fishingSpotId))
    );
  }
}
