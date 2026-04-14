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
