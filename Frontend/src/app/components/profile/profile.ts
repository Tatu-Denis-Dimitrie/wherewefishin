import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { UserService } from '../../services/user.service';
import { AuthService } from '../../services/auth.service';
import { User, UpdateUser } from '../../models/user.model';

@Component({
  selector: 'app-profile',
  imports: [CommonModule, FormsModule],
  templateUrl: './profile.html',
  styleUrl: './profile.css'
})
export class Profile implements OnInit {
  user: User | null = null;
  isEditing = false;
  loading = false;
  error = '';
  successMessage = '';
  
  editForm: UpdateUser = {
    firstName: '',
    lastName: '',
    profilePictureUrl: ''
  };

  constructor(
    private userService: UserService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }
    
    this.loadUserProfile();
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
}
