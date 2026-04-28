import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { catchError } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { AdminService, AdminStats } from '../../services/admin.service';
import { UserService } from '../../services/user.service';
import { User } from '../../models/user.model';
import { FishingSpot } from '../../models/fishing-spot.model';
import { PagedResponse } from '../../models/video-analysis.model';

type PaginationItem = number | 'ellipsis-left' | 'ellipsis-right';

@Component({
  selector: 'app-admin',
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.html',
  styleUrl: './admin.css'
})
export class Admin implements OnInit {
  stats: AdminStats | null = null;
  users: User[] = [];
  managers: User[] = [];
  fishingSpots: FishingSpot[] = [];
  loading = true;
  error = '';
  successMessage = '';
  editingSpotId: number | null = null;
  editingSpotPrice: number = 0;
  spotManagerSelections: Record<number, number | null> = {};
  savingManagerSpotId: number | null = null;
  readonly pageSize = 10;
  usersCurrentPage = 1;
  usersTotalItems = 0;
  usersTotalPages = 0;
  usersHasPreviousPage = false;
  usersHasNextPage = false;
  spotsCurrentPage = 1;
  spotsTotalItems = 0;
  spotsTotalPages = 0;
  spotsHasPreviousPage = false;
  spotsHasNextPage = false;

  get activeUsersCount(): number {
    return this.stats?.totalUsers ?? this.users.filter(user => user.isActive).length;
  }

  get disabledUsersCount(): number {
    return this.stats?.deactivatedUsers ?? this.users.filter(user => !user.isActive).length;
  }

  get totalAccountsCount(): number {
    return this.stats ? this.stats.totalUsers + this.stats.deactivatedUsers : this.usersTotalItems;
  }

  get totalSpotItems(): number {
    return this.stats?.totalSpots ?? this.spotsTotalItems;
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
    return this.managers;
  }

  get userPageStartItem(): number {
    if (!this.usersTotalItems) return 0;
    return (this.usersCurrentPage - 1) * this.pageSize + 1;
  }

  get userPageEndItem(): number {
    if (!this.users.length) return 0;
    return this.userPageStartItem + this.users.length - 1;
  }

  get spotPageStartItem(): number {
    if (!this.spotsTotalItems) return 0;
    return (this.spotsCurrentPage - 1) * this.pageSize + 1;
  }

  get spotPageEndItem(): number {
    if (!this.fishingSpots.length) return 0;
    return this.spotPageStartItem + this.fishingSpots.length - 1;
  }

  constructor(
    private authService: AuthService,
    private adminService: AdminService,
    private userService: UserService,
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
    forkJoin({
      stats: this.adminService.getStats().pipe(
        catchError(() => {
          this.error = 'Failed to load stats';
          return of(null);
        })
      ),
      usersPage: this.adminService.getUsers(this.usersCurrentPage, this.pageSize).pipe(
        catchError(() => {
          this.error = 'Failed to load users';
          return of(null);
        })
      ),
      spotsPage: this.adminService.getFishingSpots(this.spotsCurrentPage, this.pageSize).pipe(
        catchError(() => {
          this.error = 'Failed to load fishing spots';
          return of(null);
        })
      ),
      managers: this.userService.getManagers().pipe(catchError(() => of([] as User[])))
    }).subscribe(({ stats, usersPage, spotsPage, managers }) => {
      this.stats = stats;
      this.managers = managers;

      if (usersPage) {
        this.applyUsersPage(usersPage);
      }

      if (spotsPage) {
        this.applySpotsPage(spotsPage);
      }

      this.loading = false;
    });
  }

  loadUsers(page = this.usersCurrentPage): void {
    this.adminService.getUsers(page, this.pageSize).subscribe({
      next: (response) => {
        if (!response.items.length && response.totalPages > 0 && response.page > response.totalPages) {
          this.loadUsers(response.totalPages);
          return;
        }

        this.applyUsersPage(response);
      },
      error: () => {
        this.error = 'Failed to load users';
        setTimeout(() => this.error = '', 3000);
      }
    });
  }

  loadFishingSpots(page = this.spotsCurrentPage): void {
    this.adminService.getFishingSpots(page, this.pageSize).subscribe({
      next: (response) => {
        if (!response.items.length && response.totalPages > 0 && response.page > response.totalPages) {
          this.loadFishingSpots(response.totalPages);
          return;
        }

        this.applySpotsPage(response);
      },
      error: () => {
        this.error = 'Failed to load fishing spots';
        setTimeout(() => this.error = '', 3000);
      }
    });
  }

  loadManagers(): void {
    this.userService.clearCache();
    this.userService.getManagers().subscribe({
      next: (managers) => {
        this.managers = managers;
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
        this.refreshStats();
        this.loadManagers();
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
        this.refreshStats();
        this.loadManagers();
        const targetPage = this.users.length === 1 && this.usersCurrentPage > 1
          ? this.usersCurrentPage - 1
          : this.usersCurrentPage;
        this.loadUsers(targetPage);
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
        this.refreshStats();
        this.loadManagers();
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
        this.refreshStats();
        const targetPage = this.fishingSpots.length === 1 && this.spotsCurrentPage > 1
          ? this.spotsCurrentPage - 1
          : this.spotsCurrentPage;
        this.loadFishingSpots(targetPage);
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
    const manager = this.managers.find(user => user.id === managerId);
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

  changeUsersPage(page: number): void {
    const nextPage = Math.min(Math.max(page, 1), Math.max(this.usersTotalPages, 1));
    if (nextPage === this.usersCurrentPage) {
      return;
    }

    this.loadUsers(nextPage);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  changeSpotsPage(page: number): void {
    const nextPage = Math.min(Math.max(page, 1), Math.max(this.spotsTotalPages, 1));
    if (nextPage === this.spotsCurrentPage) {
      return;
    }

    this.loadFishingSpots(nextPage);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  get userVisiblePageItems(): PaginationItem[] {
    return this.buildVisiblePageItems(this.usersCurrentPage, this.usersTotalPages);
  }

  get spotVisiblePageItems(): PaginationItem[] {
    return this.buildVisiblePageItems(this.spotsCurrentPage, this.spotsTotalPages);
  }

  isPageNumber(item: PaginationItem): item is number {
    return typeof item === 'number';
  }

  private buildVisiblePageItems(currentPage: number, totalPages: number): PaginationItem[] {
    if (totalPages <= 5) {
      return Array.from({ length: totalPages }, (_, index) => index + 1);
    }

    const items: PaginationItem[] = [1];
    const startPage = Math.max(2, currentPage - 1);
    const endPage = Math.min(totalPages - 1, currentPage + 1);

    if (startPage > 2) {
      items.push('ellipsis-left');
    }

    for (let page = startPage; page <= endPage; page++) {
      items.push(page);
    }

    if (endPage < totalPages - 1) {
      items.push('ellipsis-right');
    }

    items.push(totalPages);
    return items;
  }

  private applyUsersPage(response: PagedResponse<User>): void {
    this.usersCurrentPage = response.page;
    this.users = response.items;
    this.usersTotalItems = response.totalItems;
    this.usersTotalPages = response.totalPages;
    this.usersHasPreviousPage = response.hasPreviousPage;
    this.usersHasNextPage = response.hasNextPage;
  }

  private applySpotsPage(response: PagedResponse<FishingSpot>): void {
    this.spotsCurrentPage = response.page;
    this.fishingSpots = response.items;
    this.spotsTotalItems = response.totalItems;
    this.spotsTotalPages = response.totalPages;
    this.spotsHasPreviousPage = response.hasPreviousPage;
    this.spotsHasNextPage = response.hasNextPage;
    this.syncSpotManagerSelections();
  }

  private refreshStats(): void {
    this.adminService.clearStatsCache();
    this.adminService.getStats().subscribe(stats => {
      this.stats = stats;
    });
  }
}
