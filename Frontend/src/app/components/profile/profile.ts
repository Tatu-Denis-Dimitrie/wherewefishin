import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { UserService } from '../../services/user.service';
import { AuthService } from '../../services/auth.service';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { FishingSpotService } from '../../services/fishing-spot.service';
import { EmployeeService } from '../../services/employee.service';
import { FishingSpot } from '../../models/fishing-spot.model';
import { AdminService, AdminStats } from '../../services/admin.service';
import { BookingService } from '../../services/booking.service';
import { User, UpdateUser } from '../../models/user.model';
import { Booking } from '../../models/booking.model';
import { VideoAnalysis } from '../../models/video-analysis.model';
import { EmployeeOverview, EmployeeRecentVerification } from '../../models/employee.model';
import { AppIcon } from '../../shared/icons/app-icon';

type ProfileTab = 'overview' | 'bookings' | 'settings';
type PaginationItem = number | 'ellipsis-left' | 'ellipsis-right';

interface ProfileInsight {
  title: string;
  subtitle: string;
  badge: string;
  badgeClass: string;
  icon: 'admin' | 'spots' | 'visited';
  iconClass: string;
}

@Component({
  selector: 'app-profile',
  imports: [CommonModule, FormsModule, RouterModule, AppIcon],
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
  employeeOverview: EmployeeOverview | null = null;
  adminStats: AdminStats | null = null;
  loadingAnalysisOverview = false;
  loadingEmployeeOverview = false;
  loadingSpots = false;
  loadingAdminStats = false;

  // Bookings
  recentBookings: Booking[] = [];
  pagedBookings: Booking[] = [];
  userBookingsCount = 0;
  loadingBookings = false;
  currentBookingsPage = 1;
  readonly bookingsPageSize = 10;
  bookingsTotalItems = 0;
  bookingsTotalPages = 0;
  hasPreviousBookingsPage = false;
  hasNextBookingsPage = false;

  constructor(
    private userService: UserService,
    private authService: AuthService,
    private videoAnalysisService: VideoAnalysisService,
    private fishingSpotService: FishingSpotService,
    private employeeService: EmployeeService,
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

    if (this.authService.isUser()) {
      this.loadBookings();
    } else {
      this.resetBookingsState();
    }
  }

  setTab(tab: ProfileTab): void {
    if (tab === 'bookings' && !this.isUser()) {
      this.activeTab = 'overview';
      return;
    }

    this.activeTab = tab;
  }

  loadBookings(): void {
    this.loadingBookings = true;
    this.bookingService.getMyBookings().subscribe({
      next: (bookings) => {
        this.recentBookings = [...bookings].sort((a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        this.applyBookingsPagination();
        this.userBookingsCount = bookings.filter(b => b.status !== 'Cancelled').length;
        this.loadingBookings = false;
      },
      error: () => {
        this.loadingBookings = false;
        this.resetBookingsState();
      }
    });
  }

  get bookingsPageNumbers(): number[] {
    return Array.from({ length: this.bookingsTotalPages }, (_, index) => index + 1);
  }

  get visibleBookingsPageItems(): PaginationItem[] {
    if (this.bookingsTotalPages <= 5) {
      return this.bookingsPageNumbers;
    }

    const items: PaginationItem[] = [1];
    const startPage = Math.max(2, this.currentBookingsPage - 1);
    const endPage = Math.min(this.bookingsTotalPages - 1, this.currentBookingsPage + 1);

    if (startPage > 2) {
      items.push('ellipsis-left');
    }

    for (let page = startPage; page <= endPage; page++) {
      items.push(page);
    }

    if (endPage < this.bookingsTotalPages - 1) {
      items.push('ellipsis-right');
    }

    items.push(this.bookingsTotalPages);
    return items;
  }

  isPageNumber(item: PaginationItem): item is number {
    return typeof item === 'number';
  }

  changeBookingsPage(page: number): void {
    const nextPage = Math.min(Math.max(page, 1), Math.max(this.bookingsTotalPages, 1));
    if (nextPage === this.currentBookingsPage) {
      return;
    }

    this.applyBookingsPagination(nextPage);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  loadRoleSpecificData(): void {
    const userId = this.authService.getUserId();
    if (!userId) return;

    if (this.authService.isEmployee()) {
      this.loadEmployeeOverview();
      this.loadEmployeeSpots();
      return;
    }

    this.loadAnalysisOverview(userId);

    if (this.authService.isManagerOrAdmin()) {
      this.loadManagedSpots(userId);
    }

    if (this.authService.isAdmin()) {
      this.loadAdminStats();
    }
  }

  private loadAnalysisOverview(userId: number): void {
    this.loadingAnalysisOverview = true;

    this.videoAnalysisService.getUserAnalysesOverview(userId).subscribe({
      next: (overview) => {
        this.userAnalysesCount = overview.totalItems;
        this.userCompletedCount = overview.completedItems;
        this.recentAnalyses = overview.recentAnalyses;
        this.loadingAnalysisOverview = false;
      },
      error: () => {
        this.loadingAnalysisOverview = false;
      }
    });
  }

  private loadEmployeeOverview(): void {
    this.loadingEmployeeOverview = true;

    this.employeeService.getMyOverview().subscribe({
      next: (overview) => {
        this.employeeOverview = overview;
        this.loadingEmployeeOverview = false;
      },
      error: () => {
        this.employeeOverview = null;
        this.loadingEmployeeOverview = false;
      }
    });
  }

  private loadEmployeeSpots(): void {
    this.loadingSpots = true;

    this.fishingSpotService.getAll().subscribe({
      next: (spots) => {
        this.userSpots = spots;
        this.userSpotsCount = spots.length;
        this.loadingSpots = false;
      },
      error: () => {
        this.userSpots = [];
        this.userSpotsCount = 0;
        this.loadingSpots = false;
      }
    });
  }

  private loadManagedSpots(userId: number): void {
    this.loadingSpots = true;

    const request = this.authService.isAdmin()
      ? this.fishingSpotService.getAll()
      : this.fishingSpotService.getManaged();

    request.subscribe({
      next: (spots) => {
        this.userSpots = spots.filter(spot => spot.managerId === userId || spot.userId === userId);
        this.userSpotsCount = this.userSpots.length;
        this.loadingSpots = false;
      },
      error: () => {
        this.loadingSpots = false;
      }
    });
  }

  private loadAdminStats(): void {
    this.loadingAdminStats = true;

    this.adminService.getStats().subscribe({
      next: (stats) => {
        this.adminStats = stats;
        this.loadingAdminStats = false;
      },
      error: () => {
        this.loadingAdminStats = false;
      }
    });
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
      error: () => {
        this.error = 'Failed to load profile';
        this.loading = false;
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
      error: () => {
        this.error = 'Failed to update profile';
        this.loading = false;
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

  get profileInsight(): ProfileInsight {
    if (
      this.loadingBookings ||
      this.loadingAnalysisOverview ||
      this.loadingEmployeeOverview ||
      (this.isEmployee() && this.loadingSpots) ||
      (this.isManager() && this.loadingSpots) ||
      (this.isAdmin() && this.loadingAdminStats)
    ) {
      return {
        title: 'Activity Insight',
        subtitle: 'Loading a live summary for your account.',
        badge: 'Syncing',
        badgeClass: 'badge-neutral',
        icon: 'visited',
        iconClass: 'sec-neutral'
      };
    }
    const joinedLabel = this.user ? `Member since ${this.formatMonthYear(this.user.createdAt)}.` : '';

    if (this.isEmployee()) {
      const verifiedCount = this.employeeOverview?.verifiedQrScansCount ?? 0;
      const verifiedToday = this.employeeOverview?.verifiedQrScansTodayCount ?? 0;
      const subtitle = verifiedCount > 0
        ? `You validated ${this.formatCountLabel(verifiedCount, 'QR session')}, with ${this.formatCountLabel(this.employeeOverview?.verifiedGuestsCount ?? 0, 'angler check-in')} and ${this.formatCountLabel(verifiedToday, 'verification today')}. ${joinedLabel}`.trim()
        : `You are currently assigned to ${this.formatCountLabel(this.userSpotsCount, 'lake')}. Your QR validation metrics will appear here after the first successful check-in. ${joinedLabel}`.trim();

      return {
        title: 'Checkpoint Crew',
        subtitle,
        badge: verifiedCount > 0 ? 'On Duty' : 'Assigned',
        badgeClass: verifiedCount > 0 ? 'badge-active' : 'badge-info',
        icon: 'spots',
        iconClass: verifiedCount > 0 ? 'sec-ok' : 'sec-info'
      };
    }

    if (this.isAdmin()) {
      const coverage = [
        this.formatCountLabel(this.adminStats?.totalUsers ?? 0, 'user account'),
        this.formatCountLabel(this.adminStats?.totalSpots ?? 0, 'fishing spot'),
        this.formatCountLabel(this.adminStats?.failedAnalyses ?? 0, 'flagged analysis')
      ].join(', ');

      return {
        title: 'Platform Pulse',
        subtitle: `Monitoring ${coverage} across the platform.`,
        badge: 'Control Room',
        badgeClass: 'badge-info',
        icon: 'admin',
        iconClass: 'sec-info'
      };
    }

    if (this.isManager()) {
      const subtitle = this.userSpotsCount > 0
        ? `You currently oversee ${this.formatCountLabel(this.userSpotsCount, 'managed spot')} and have ${this.formatCountLabel(this.userCompletedCount, 'completed analysis')}. ${joinedLabel}`.trim()
        : `Your manager access is ready. Add your first lake to start building your management footprint. ${joinedLabel}`.trim();

      return {
        title: 'Lake Steward',
        subtitle,
        badge: this.userSpotsCount > 0 ? 'Steward' : 'Ready',
        badgeClass: this.userSpotsCount > 0 ? 'badge-active' : 'badge-neutral',
        icon: 'spots',
        iconClass: this.userSpotsCount > 0 ? 'sec-ok' : 'sec-neutral'
      };
    }

    const activityScore = this.userBookingsCount * 3 + this.userCompletedCount * 2 + Math.min(this.userAnalysesCount, 4);
    let badge = 'Newcomer';
    let badgeClass = 'badge-neutral';
    let iconClass = 'sec-neutral';

    if (activityScore >= 12) {
      badge = 'Dedicated';
      badgeClass = 'badge-active';
      iconClass = 'sec-ok';
    } else if (activityScore >= 6) {
      badge = 'Explorer';
      badgeClass = 'badge-info';
      iconClass = 'sec-info';
    }

    const subtitle = activityScore === 0
      ? `Start with a booking or an AI analysis and this space will evolve with your real activity. ${joinedLabel}`.trim()
      : `Built from ${this.formatCountLabel(this.userBookingsCount, 'active booking')}, ${this.formatCountLabel(this.userCompletedCount, 'completed analysis')}, and ${this.formatCountLabel(this.userAnalysesCount, 'total analysis')}. ${joinedLabel}`.trim();

    return {
      title: 'Fishing Persona',
      subtitle,
      badge,
      badgeClass,
      icon: 'visited',
      iconClass
    };
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }

  formatMonthYear(date: Date): string {
    return new Intl.DateTimeFormat('en-US', {
      month: 'long',
      year: 'numeric'
    }).format(new Date(date));
  }

  isAdmin(): boolean {
    return this.authService.isAdmin();
  }

  isManager(): boolean {
    return this.user?.role === 'Manager' || this.authService.isManager();
  }

  isUser(): boolean {
    return this.user?.role === 'User' || this.authService.isUser();
  }

  isEmployee(): boolean {
    return this.user?.role === 'Employee' || this.authService.isEmployee();
  }

  get recentEmployeeVerifications(): EmployeeRecentVerification[] {
    return this.employeeOverview?.recentVerifications ?? [];
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
    return new Date(date).toLocaleDateString('en-US', { day: 'numeric', month: 'short', year: 'numeric' });
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
    return price.toLocaleString('en-US', { style: 'currency', currency: 'RON', maximumFractionDigits: 0 });
  }

  private resetBookingsState(): void {
    this.recentBookings = [];
    this.pagedBookings = [];
    this.userBookingsCount = 0;
    this.loadingBookings = false;
    this.currentBookingsPage = 1;
    this.bookingsTotalItems = 0;
    this.bookingsTotalPages = 0;
    this.hasPreviousBookingsPage = false;
    this.hasNextBookingsPage = false;
  }

  private formatCountLabel(count: number, singular: string, plural = `${singular}s`): string {
    return `${count} ${count === 1 ? singular : plural}`;
  }

  private applyBookingsPagination(page = this.currentBookingsPage): void {
    this.bookingsTotalItems = this.recentBookings.length;
    this.bookingsTotalPages = Math.ceil(this.bookingsTotalItems / this.bookingsPageSize);
    this.currentBookingsPage = this.bookingsTotalItems
      ? Math.min(Math.max(page, 1), this.bookingsTotalPages)
      : 1;

    const startIndex = (this.currentBookingsPage - 1) * this.bookingsPageSize;
    this.pagedBookings = this.recentBookings.slice(startIndex, startIndex + this.bookingsPageSize);
    this.hasPreviousBookingsPage = this.currentBookingsPage > 1;
    this.hasNextBookingsPage = this.currentBookingsPage < this.bookingsTotalPages;
  }
}
