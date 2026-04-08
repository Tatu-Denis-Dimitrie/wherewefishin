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
  spotManagerSelections: Record<number, number | null> = {};
  savingManagerSpotId: number | null = null;

  get activeUsersCount(): number {
    return this.stats?.totalUsers ?? this.users.filter(user => user.isActive).length;
  }

  get disabledUsersCount(): number {
    return this.stats?.deactivatedUsers ?? this.users.filter(user => !user.isActive).length;
  }

  get totalAccountsCount(): number {
    return this.activeUsersCount + this.disabledUsersCount;
  }

  get managedSpotsCount(): number {
    return this.fishingSpots.filter(spot => spot.managerId != null).length;
  }

  get unmanagedSpotsCount(): number {
    return Math.max(0, this.fishingSpots.length - this.managedSpotsCount);
  }

  get averageSpotPrice(): number {
    if (this.fishingSpots.length === 0) return 0;
    const total = this.fishingSpots.reduce((sum, spot) => sum + spot.pricePerHour, 0);
    return total / this.fishingSpots.length;
  }

  get managerOptions(): User[] {
    return this.users.filter(user => user.role === 'Manager');
  }

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
        this.syncSpotManagerSelections();
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

  toggleStatus(user: User): void {
    if (user.id === this.authService.getUserId()) {
      this.error = 'You cannot disable yourself';
      setTimeout(() => this.error = '', 3000);
      return;
    }

    const enable = !user.isActive;
    this.adminService.toggleUserStatus(user.id, enable).subscribe({
      next: () => {
        user.isActive = enable;
        this.successMessage = `User "${user.username}" ${enable ? 'enabled' : 'disabled'}`;
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: () => {
        this.error = `Failed to ${enable ? 'enable' : 'disable'} user`;
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

  getSelectedManagerId(spot: FishingSpot): number | null {
    return this.spotManagerSelections[spot.id] ?? spot.managerId ?? null;
  }

  updateSelectedManager(spotId: number, managerId: number | null): void {
    this.spotManagerSelections[spotId] = managerId;
  }

  hasManagerSelectionChanged(spot: FishingSpot): boolean {
    return this.getSelectedManagerId(spot) !== (spot.managerId ?? null);
  }

  canSaveManager(spot: FishingSpot): boolean {
    return this.getSelectedManagerId(spot) !== null && this.hasManagerSelectionChanged(spot);
  }

  saveFishingSpotManager(spot: FishingSpot): void {
    const managerId = this.getSelectedManagerId(spot);
    if (managerId === null) {
      this.error = 'Please select a manager';
      setTimeout(() => this.error = '', 3000);
      return;
    }

    this.savingManagerSpotId = spot.id;
    this.adminService.updateFishingSpot(spot.id, { managerId }).subscribe({
      next: () => {
        spot.managerId = managerId;
        spot.managerName = this.getManagerDisplayName(managerId);
        this.spotManagerSelections[spot.id] = managerId;
        this.savingManagerSpotId = null;
        this.successMessage = `Manager updated for "${spot.name}"`;
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: () => {
        this.savingManagerSpotId = null;
        this.error = 'Failed to update fishing spot manager';
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
      case 'Employee': return 'badge-employee';
      default: return 'badge-user';
    }
  }

  getManagerOptionLabel(manager: User): string {
    const fullName = `${manager.firstName ?? ''} ${manager.lastName ?? ''}`.trim();
    const primaryLabel = fullName ? `${fullName} (@${manager.username})` : manager.username;
    return manager.isActive ? primaryLabel : `${primaryLabel} - disabled`;
  }

  private getManagerDisplayName(managerId: number): string {
    const manager = this.users.find(user => user.id === managerId);
    if (!manager) return 'Assigned manager';

    const fullName = `${manager.firstName ?? ''} ${manager.lastName ?? ''}`.trim();
    return fullName || manager.username;
  }

  private syncSpotManagerSelections(): void {
    const nextSelections: Record<number, number | null> = {};
    for (const spot of this.fishingSpots) {
      nextSelections[spot.id] = spot.managerId ?? null;
    }
    this.spotManagerSelections = nextSelections;
  }
}
