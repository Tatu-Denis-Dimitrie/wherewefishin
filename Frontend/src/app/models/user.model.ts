export interface User {
  id: number;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  profilePictureUrl?: string;
  role: string;
  createdAt: Date;
  isActive: boolean;
}

export interface UpdateUser {
  firstName?: string;
  lastName?: string;
  profilePictureUrl?: string;
}
