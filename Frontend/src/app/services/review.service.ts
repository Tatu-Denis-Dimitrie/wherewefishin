import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
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

  constructor(private http: HttpClient) {}

  getSpotReviews(fishingSpotId: number): Observable<Review[]> {
    return this.http.get<Review[]>(`${this.apiUrl}/spot/${fishingSpotId}`);
  }

  getAverageRating(fishingSpotId: number): Observable<ReviewStats> {
    return this.http.get<ReviewStats>(`${this.apiUrl}/spot/${fishingSpotId}/average`);
  }

  getReview(id: number): Observable<Review> {
    return this.http.get<Review>(`${this.apiUrl}/${id}`);
  }

  createReview(review: CreateReview): Observable<Review> {
    return this.http.post<Review>(this.apiUrl, review);
  }

  updateReview(id: number, review: UpdateReview): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, review);
  }

  deleteReview(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
