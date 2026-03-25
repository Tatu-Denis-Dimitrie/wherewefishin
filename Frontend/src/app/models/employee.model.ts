export interface SpotEmployee {
  id: number;
  userId: number;
  username: string;
  firstName?: string;
  lastName?: string;
  fishingSpotId: number;
  fishingSpotName: string;
  createdAt: string;
}

export interface AssignEmployeeRequest {
  userId: number;
  fishingSpotId: number;
}

export interface VerifyQrRequest {
  bookingId: number;
  verificationToken: string;
}

export interface QrVerificationResult {
  valid: boolean;
  message: string;
  bookingId?: number;
  username?: string;
  fishingSpotName?: string;
  pontoonName?: string;
  startDate?: string;
  durationHours?: number;
  totalPrice?: number;
  status?: string;
}
