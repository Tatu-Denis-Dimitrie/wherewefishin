import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { AdminService, AdminStats } from '../../services/admin.service';
import { User } from '../../models/user.model';

@Component({
  selector: 'app-admin',
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.html',
  styleUrl: './admin.css'
})
export class Admin implements OnInit {
  stats: AdminStats | null = null;
  users: User[] = [];
  loading = true;
  error = '';
  successMessage = '';

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

  getRoleBadgeClass(role: string): string {
    switch (role) {
      case 'Admin': return 'badge-admin';
      case 'Manager': return 'badge-manager';
      default: return 'badge-user';
    }
  }

  goBack(): void {
    this.router.navigate(['/home']);
  }

  logout(): void {
    this.authService.logout();
  }
}
