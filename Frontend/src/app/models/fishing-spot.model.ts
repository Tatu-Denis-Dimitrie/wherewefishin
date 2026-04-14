export interface FishingSpot {
  id: number;
  name: string;
  description?: string;
  latitude: number;
  longitude: number;
  imageUrl?: string;
  pricePerHour: number;
  userId: number;
  managerId?: number;
  managerName?: string;
  defaultZoom?: number;
  defaultCenterLat?: number;
  defaultCenterLng?: number;
  fishSpecies?: string;
  createdAt: Date;
}

export interface CreateFishingSpot {
  name: string;
  description?: string;
  latitude: number;
  longitude: number;
  pricePerHour?: number;
  managerId?: number;
}

export interface UpdateFishingSpot {
  name?: string;
  description?: string;
  latitude?: number;
  longitude?: number;
  imageUrl?: string;
  pricePerHour?: number;
  managerId?: number;
  clearManager?: boolean;
  defaultZoom?: number;
  defaultCenterLat?: number;
  defaultCenterLng?: number;
  resetDefaultMapView?: boolean;
  fishSpecies?: string;
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
