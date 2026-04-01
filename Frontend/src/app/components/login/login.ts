import { ChangeDetectorRef, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

type View = 'login' | 'forgot' | 'reset' | 'done';

@Component({
  selector: 'app-login',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  view: View = 'login';

  loginForm: FormGroup;
  forgotForm: FormGroup;
  resetForm: FormGroup;

  errorMessage = '';
  successMessage = '';
  isLoading = false;

  private resetEmail = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {
    this.loginForm = this.fb.group({
      usernameOrEmail: ['', [Validators.required]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });

    this.forgotForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });

    this.resetForm = this.fb.group({
      code: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]],
      newPassword: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  onSubmit(): void {
    if (this.isLoading || this.loginForm.invalid) return;

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login(this.loginForm.value).subscribe({
      next: () => {
        this.isLoading = false;
        this.router.navigate(['/home']);
      },
      error: (error) => {
        this.isLoading = false;
        this.errorMessage = error.error?.message || 'Invalid username or password';
        this.cdr.detectChanges();
      }
    });
  }

  onForgotSubmit(): void {
    if (this.forgotForm.valid) {
      this.isLoading = true;
      this.errorMessage = '';
      this.resetEmail = this.forgotForm.value.email;

      this.authService.forgotPassword(this.resetEmail).subscribe({
        next: () => {
          this.isLoading = false;
          this.view = 'reset';
        },
        error: () => {
          this.isLoading = false;
          this.view = 'reset';
        }
      });
    }
  }

  onResetSubmit(): void {
    if (this.resetForm.valid) {
      this.isLoading = true;
      this.errorMessage = '';

      this.authService.resetPassword({
        email: this.resetEmail,
        code: this.resetForm.value.code,
        newPassword: this.resetForm.value.newPassword
      }).subscribe({
        next: () => {
          this.isLoading = false;
          this.view = 'done';
        },
        error: (error) => {
          this.isLoading = false;
          this.errorMessage = error.error?.message || 'Invalid or expired code. Please try again.';
        }
      });
    }
  }

  goToForgot(): void {
    this.forgotForm.reset();
    this.errorMessage = '';
    this.view = 'forgot';
  }

  goToLogin(): void {
    this.loginForm.reset();
    this.forgotForm.reset();
    this.resetForm.reset();
    this.errorMessage = '';
    this.view = 'login';
  }

  get usernameOrEmail() { return this.loginForm.get('usernameOrEmail'); }
  get password() { return this.loginForm.get('password'); }
  get email() { return this.forgotForm.get('email'); }
  get code() { return this.resetForm.get('code'); }
  get newPassword() { return this.resetForm.get('newPassword'); }
}
