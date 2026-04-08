export interface FishStocking {
  id: number;
  fishingSpotId: number;
  stockingDate: string;
  species: string;
  quantity: number;
  notes?: string;
  createdAt: string;
}

export interface CreateFishStocking {
  stockingDate: string;
  species: string;
  quantity: number;
  notes?: string;
}

export interface SpotStatistics {
  totalBookings: number;
  activeBookings: number;
  cancelledBookings: number;
  totalRevenue: number;
  totalReviews: number;
  averageRating?: number;
  totalPontoons: number;
  totalEmployees: number;
  totalStockings: number;
}
