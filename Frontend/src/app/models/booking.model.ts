export interface CartItem {
  spotId: number;
  spotName: string;
  pontoonId?: number;
  pontoonName?: string;
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
  pontoonId?: number;
  pontoonName?: string;
  startDate: string;
  durationHours: number;
  totalPrice: number;
  status: string;
  verificationToken?: string;
  createdAt: string;
}

export interface CreateBookingRequest {
  fishingSpotId: number;
  pontoonId?: number;
  startDate: string;
  durationHours: number;
  paymentIntentId?: string;
}

export interface CreatePaymentIntentRequest {
  fishingSpotId: number;
  pontoonId?: number;
  startDate: string;
  durationHours: number;
}

export interface PaymentIntentResponse {
  paymentIntentId: string;
  clientSecret: string;
  amount: number;
  currency: string;
}

export interface PaymentConfiguration {
  stripeEnabled: boolean;
}

export interface BookedPeriod {
  startDate: string;
  endDate: string;
}
