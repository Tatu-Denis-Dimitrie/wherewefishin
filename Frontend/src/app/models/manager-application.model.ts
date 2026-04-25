export interface ManagerApplication {
  id: number;
  applicantUserId: number;
  applicantUsername: string;
  applicantDisplayName: string;
  lakeName: string;
  description?: string;
  latitude: number;
  longitude: number;
  locationLabel?: string;
  proposedPricePerHour: number;
  fishSpecies?: string;
  contactPhone: string;
  motivation: string;
  administrationBasis: string;
  status: string;
  rejectionReason?: string;
  reviewedAt?: string;
  reviewedByAdminId?: number;
  reviewedByAdminName?: string;
  approvedFishingSpotId?: number;
  createdAt: string;
  updatedAt?: string;
}

export interface UpsertManagerApplication {
  lakeName: string;
  description?: string;
  latitude: number;
  longitude: number;
  locationLabel?: string;
  proposedPricePerHour: number;
  fishSpecies?: string;
  contactPhone: string;
  motivation: string;
  administrationBasis: string;
}

export interface RejectManagerApplication {
  reason: string;
}

export interface AdminHomeOverview {
  activeUsers: number;
  deactivatedUsers: number;
  totalSpots: number;
  spotsWithoutManager: number;
  pendingManagerApplications: number;
  rejectedManagerApplications: number;
  failedVideoAnalyses: number;
  cancelledBookings: number;
  pendingApplications: ManagerApplication[];
}