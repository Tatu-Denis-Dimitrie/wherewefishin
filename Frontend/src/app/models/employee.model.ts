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

export interface EmployeeRecentVerification {
  bookingId: number;
  fishingSpotId: number;
  fishingSpotName: string;
  username: string;
  verifiedAt: string;
  startDate: string;
  durationHours: number;
}

export interface EmployeeOverview {
  assignedSpotsCount: number;
  verifiedQrScansCount: number;
  verifiedQrScansTodayCount: number;
  verifiedGuestsCount: number;
  activeAssignedBookingsCount: number;
  recentVerifications: EmployeeRecentVerification[];
}
