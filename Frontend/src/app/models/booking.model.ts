export interface CartItem {
  spotId: number;
  spotName: string;
  latitude: number;
  longitude: number;
  pricePerHour: number;
  durationHours: number;
  startDate: string; // ISO string
}

export interface Booking {
  id: number;
  userId: number;
  fishingSpotId: number;
  fishingSpotName: string;
  startDate: string;
  durationHours: number;
  totalPrice: number;
  status: string;
  createdAt: string;
}

export interface CreateBookingRequest {
  fishingSpotId: number;
  startDate: string;
  durationHours: number;
}
