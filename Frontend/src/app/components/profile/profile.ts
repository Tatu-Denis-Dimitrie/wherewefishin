import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { UserService } from '../../services/user.service';
import { AuthService } from '../../services/auth.service';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { FishingSpotService, FishingSpot } from '../../services/fishing-spot.service';
import { AdminService, AdminStats } from '../../services/admin.service';
import { BookingService } from '../../services/booking.service';
import { User, UpdateUser } from '../../models/user.model';
import { Booking } from '../../models/booking.model';
import { VideoAnalysis } from '../../models/video-analysis.model';

type ProfileTab = 'overview' | 'bookings' | 'settings';

@Component({
  selector: 'app-profile',
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './profile.html',
  styleUrl: './profile.css'
})
export class Profile implements OnInit {
  user: User | null = null;
  isEditing = false;
  loading = false;
  error = '';
  successMessage = '';

  activeTab: ProfileTab = 'overview';

  editForm: UpdateUser = {
    firstName: '',
    lastName: '',
    profilePictureUrl: ''
  };

  // Password change modal
  showPasswordModal = false;
  passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
  passwordError = '';

  // Role-specific data
  userAnalysesCount = 0;
  userCompletedCount = 0;
  recentAnalyses: VideoAnalysis[] = [];
  userSpotsCount = 0;
  userSpots: FishingSpot[] = [];
  adminStats: AdminStats | null = null;
  loadingStats = false;

  // Bookings
  recentBookings: Booking[] = [];
  userBookingsCount = 0;
  loadingBookings = false;

  constructor(
    private userService: UserService,
    private authService: AuthService,
    private videoAnalysisService: VideoAnalysisService,
    private fishingSpotService: FishingSpotService,
    private adminService: AdminService,
    private bookingService: BookingService,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }
    
    this.loadUserProfile();
    this.loadRoleSpecificData();
    this.loadBookings();
  }

  setTab(tab: ProfileTab): void {
    this.activeTab = tab;
  }

  loadBookings(): void {
    this.loadingBookings = true;
    this.bookingService.getMyBookings().subscribe({
      next: (bookings) => {
        this.recentBookings = [...bookings].sort((a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        this.userBookingsCount = bookings.filter(b => b.status !== 'Cancelled').length;
        this.loadingBookings = false;
      },
      error: () => { this.loadingBookings = false; }
    });
  }

  loadRoleSpecificData(): void {
    const userId = this.authService.getUserId();
    if (!userId) return;

    this.loadingStats = true;

    // Load user's video analyses (for all roles)
    this.videoAnalysisService.getUserAnalyses(userId).subscribe({
      next: (analyses) => {
        this.userAnalysesCount = analyses.length;
        this.userCompletedCount = analyses.filter(a => a.status === 'Completed').length;
        this.recentAnalyses = analyses.slice(0, 3);
        this.loadingStats = false;
      },
      error: () => {
        this.loadingStats = false;
      }
    });

    // Load fishing spots for Manager and Admin
    if (this.authService.isManagerOrAdmin()) {
      this.fishingSpotService.getAll().subscribe({
        next: (spots) => {
          // Filter spots where user is manager or owner
          this.userSpots = spots.filter(s => s.managerId === userId || s.userId === userId);
          this.userSpotsCount = this.userSpots.length;
        },
        error: () => {}
      });
    }

    // Load admin stats for Admin only
    if (this.authService.isAdmin()) {
      this.adminService.getStats().subscribe({
        next: (stats) => {
          this.adminStats = stats;
        },
        error: () => {}
      });
    }
  }

  loadUserProfile(): void {
    const userId = this.authService.getUserId();
    if (!userId) {
      this.error = 'User ID not found';
      return;
    }

    this.loading = true;
    this.userService.getUser(userId).subscribe({
      next: (user) => {
        this.user = user;
        this.editForm = {
          firstName: user.firstName || '',
          lastName: user.lastName || '',
          profilePictureUrl: user.profilePictureUrl || ''
        };
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load profile';
        this.loading = false;
        console.error('Error loading profile:', err);
      }
    });
  }

  toggleEdit(): void {
    if (this.isEditing) {
      // Cancel editing
      if (this.user) {
        this.editForm = {
          firstName: this.user.firstName || '',
          lastName: this.user.lastName || '',
          profilePictureUrl: this.user.profilePictureUrl || ''
        };
      }
    }
    this.isEditing = !this.isEditing;
    this.error = '';
    this.successMessage = '';
  }

  saveProfile(): void {
    const userId = this.authService.getUserId();
    if (!userId) {
      this.error = 'User ID not found';
      return;
    }

    this.loading = true;
    this.error = '';
    
    this.userService.updateUser(userId, this.editForm).subscribe({
      next: () => {
        this.successMessage = 'Profile updated successfully!';
        this.isEditing = false;
        this.loading = false;
        this.loadUserProfile();
        
        setTimeout(() => {
          this.successMessage = '';
        }, 3000);
      },
      error: (err) => {
        this.error = 'Failed to update profile';
        this.loading = false;
        console.error('Error updating profile:', err);
      }
    });
  }

  getInitials(): string {
    if (!this.user) return '?';
    
    const first = this.user.firstName?.charAt(0) || '';
    const last = this.user.lastName?.charAt(0) || '';
    
    if (first || last) {
      return (first + last).toUpperCase();
    }
    
    return this.user.username.charAt(0).toUpperCase();
  }

  getDisplayName(): string {
    if (!this.user) return '';
    
    if (this.user.firstName || this.user.lastName) {
      return `${this.user.firstName || ''} ${this.user.lastName || ''}`.trim();
    }
    
    return this.user.username;
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('ro-RO', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }

  isAdmin(): boolean {
    return this.authService.isAdmin();
  }

  isManager(): boolean {
    return this.user?.role === 'Manager';
  }

  isUser(): boolean {
    return this.user?.role === 'User';
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'completed': return 'status-completed';
      case 'processing': return 'status-processing';
      case 'failed': return 'status-failed';
      default: return 'status-pending';
    }
  }

  openChangePassword(): void {
    this.passwordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
    this.passwordError = '';
    this.showPasswordModal = true;
  }

  closeChangePassword(): void {
    this.showPasswordModal = false;
    this.passwordError = '';
  }

  changePassword(): void {
    const { currentPassword, newPassword, confirmPassword } = this.passwordForm;
    if (!currentPassword || !newPassword || !confirmPassword) {
      this.passwordError = 'All fields are required.';
      return;
    }
    if (newPassword !== confirmPassword) {
      this.passwordError = 'Passwords do not match.';
      return;
    }
    if (newPassword.length < 6) {
      this.passwordError = 'New password must be at least 6 characters.';
      return;
    }
    const userId = this.authService.getUserId();
    if (!userId) return;

    this.loading = true;
    this.passwordError = '';
    this.userService.changePassword(userId, { currentPassword, newPassword }).subscribe({
      next: () => {
        this.showPasswordModal = false;
        this.successMessage = 'Password changed successfully!';
        this.loading = false;
        setTimeout(() => { this.successMessage = ''; }, 3000);
      },
      error: (err) => {
        this.passwordError = err.error?.message ?? 'Incorrect current password.';
        this.loading = false;
      }
    });
  }

  logout(): void {
    this.authService.logout();
  }

  formatBookingDate(date: string): string {
    return new Date(date).toLocaleDateString('ro-RO', { day: 'numeric', month: 'short', year: 'numeric' });
  }

  getBookingStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'confirmed': return 'bstatus-confirmed';
      case 'cancelled': return 'bstatus-cancelled';
      case 'pending': return 'bstatus-pending';
      case 'completed': return 'bstatus-done';
      default: return 'bstatus-pending';
    }
  }

  formatPrice(price: number): string {
    return price.toLocaleString('ro-RO', { style: 'currency', currency: 'RON', maximumFractionDigits: 0 });
  }
}
