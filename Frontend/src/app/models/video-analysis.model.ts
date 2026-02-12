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

export interface SupportedFishResponse {
  fishTypes: string[];
  total: number;
}
