import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { AdminService, AdminStats } from '../../services/admin.service';
import { User } from '../../models/user.model';
import { FishingSpot } from '../../services/fishing-spot.service';

@Component({
  selector: 'app-admin',
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.html',
  styleUrl: './admin.css'
})
export class Admin implements OnInit {
  stats: AdminStats | null = null;
  users: User[] = [];
  fishingSpots: FishingSpot[] = [];
  loading = true;
  error = '';
  successMessage = '';
  editingSpotId: number | null = null;
  editingSpotPrice: number = 0;

  constructor(
    private authService: AuthService,
    private adminService: AdminService,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (!this.authService.isAdmin()) {
      this.router.navigate(['/home']);
      return;
    }
    this.loadData();
  }

  loadData(): void {
    this.loading = true;
    this.adminService.getStats().subscribe({
      next: (stats) => {
        this.stats = stats;
      },
      error: () => this.error = 'Failed to load stats'
    });

    this.adminService.getUsers().subscribe({
      next: (users) => {
        this.users = users;
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load users';
        this.loading = false;
      }
    });

    this.adminService.getFishingSpots().subscribe({
      next: (spots) => {
        this.fishingSpots = spots;
      },
      error: () => {
        this.error = 'Failed to load fishing spots';
      }
    });
  }

  changeRole(user: User, newRole: string): void {
    if (user.id === this.authService.getUserId()) {
      this.error = 'You cannot change your own role';
      setTimeout(() => this.error = '', 3000);
      return;
    }

    this.adminService.updateUserRole(user.id, newRole).subscribe({
      next: () => {
        user.role = newRole;
        this.successMessage = `${user.username} is now ${newRole}`;
        this.loadData();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: () => {
        this.error = 'Failed to update role';
        setTimeout(() => this.error = '', 3000);
      }
    });
  }

  deleteUser(user: User): void {
    if (user.id === this.authService.getUserId()) {
      this.error = 'You cannot delete yourself';
      setTimeout(() => this.error = '', 3000);
      return;
    }
    if (!confirm(`Delete user "${user.username}"? This cannot be undone.`)) return;

    this.adminService.deleteUser(user.id).subscribe({
      next: () => {
        this.successMessage = `User "${user.username}" deleted`;
        this.loadData();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: () => {
        this.error = 'Failed to delete user';
        setTimeout(() => this.error = '', 3000);
      }
    });
  }

  startEditingPrice(spot: FishingSpot): void {
    this.editingSpotId = spot.id;
    this.editingSpotPrice = spot.pricePerHour;
  }

  cancelEditingPrice(): void {
    this.editingSpotId = null;
    this.editingSpotPrice = 0;
  }

  saveFishingSpotPrice(spot: FishingSpot): void {
    if (this.editingSpotPrice < 0) {
      this.error = 'Price cannot be negative';
      setTimeout(() => this.error = '', 3000);
      return;
    }

    this.adminService.updateFishingSpot(spot.id, { 
      pricePerHour: this.editingSpotPrice 
    }).subscribe({
      next: () => {
        spot.pricePerHour = this.editingSpotPrice;
        this.editingSpotId = null;
        this.successMessage = `Price updated for "${spot.name}"`;
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: () => {
        this.error = 'Failed to update fishing spot price';
        setTimeout(() => this.error = '', 3000);
      }
    });
  }

  deleteFishingSpot(spot: FishingSpot): void {
    if (!confirm(`Delete fishing spot "${spot.name}"? This cannot be undone.`)) return;

    this.adminService.deleteFishingSpot(spot.id).subscribe({
      next: () => {
        this.successMessage = `Fishing spot "${spot.name}" deleted`;
        this.loadData();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: () => {
        this.error = 'Failed to delete fishing spot';
        setTimeout(() => this.error = '', 3000);
      }
    });
  }

  getRoleBadgeClass(role: string): string {
    switch (role) {
      case 'Admin': return 'badge-admin';
      case 'Manager': return 'badge-manager';
      default: return 'badge-user';
    }
  }
}
