export interface VideoAnalysis {
  id: number;
  userId: number;
  fileName: string;
  videoUrl: string;
  processedVideoUrl?: string;
  duration: number;
  totalFrames: number;
  fps: number;
  totalDetections: number;
  totalUniqueFish?: number;
  dominantFishType?: string;
  dominantFishCount: number;
  fishCounts?: { [key: string]: number };
  detections?: FishDetection[];
  analyzedAt: Date;
  status: string;
  errorMessage?: string;
  createdAt: Date;
}

export interface FishDetection {
  fishType: string;
  confidence: number;
  timestamp: number;
  frameNumber: number;
  trackId?: number;
  bbox: BoundingBox;
}

export interface BoundingBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface AnalysisResult {
  success: boolean;
  analysis?: VideoAnalysis;
  error?: string;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface VideoAnalysisOverview {
  totalItems: number;
  completedItems: number;
  recentAnalyses: VideoAnalysis[];
}

export interface SupportedFishResponse {
  fishTypes: string[];
  total: number;
}

export interface ClassProbability {
  fishType: string;
  confidence: number;
}

export interface ImageDetection {
  fishType: string;
  confidence: number;
  bbox: BoundingBox;
  classProbabilities?: ClassProbability[];
}

export interface ImageAnalysisResult {
  success: boolean;
  id?: number;
  userId?: number;
  fileName?: string;
  detections?: ImageDetection[];
  dominantDetection?: ImageDetection;
  processedImageUrl?: string;
  totalDetections: number;
  analyzedAt?: Date;
  createdAt?: Date;
  error?: string;
}

export interface ImageAnalysisSummary {
  id: number;
  userId: number;
  fileName: string;
  processedImageUrl?: string;
  totalDetections: number;
  dominantFishType?: string;
  dominantConfidence: number;
  detections?: ImageDetection[];
  analyzedAt: Date;
  createdAt: Date;
}
