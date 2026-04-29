import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export class Register {
  registerForm: FormGroup;
  errorMessage = '';
  isLoading = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.registerForm = this.fb.group({
      username: ['', [Validators.required, Validators.minLength(3)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required]],
      firstName: [''],
      lastName: ['']
    });

    this.username?.valueChanges.subscribe(() => {
      this.clearServerFieldError(this.username, 'duplicateTaken');
      this.errorMessage = '';
    });

    this.email?.valueChanges.subscribe(() => {
      this.clearServerFieldError(this.email, 'duplicateTaken');
      this.errorMessage = '';
    });
  }

  onSubmit(): void {
    if (this.registerForm.valid) {
      this.clearRegistrationConflictErrors();

      const password = this.registerForm.value.password;
      const confirmPassword = this.registerForm.value.confirmPassword;
      
      if (password !== confirmPassword) {
        this.errorMessage = 'Passwords do not match';
        return;
      }

      this.isLoading = true;
      this.errorMessage = '';

      this.authService.register(this.registerForm.value).subscribe({
        next: () => {
          this.isLoading = false;
          this.router.navigate(['/']);
        },
        error: (error: HttpErrorResponse) => {
          this.isLoading = false;

          if (this.applyRegistrationConflictErrors(error)) {
            return;
          }

          this.errorMessage = error.error?.message || 'An error occurred during registration';
        }
      });
    }
  }

  private applyRegistrationConflictErrors(error: HttpErrorResponse): boolean {
    const message = typeof error.error?.message === 'string' ? error.error.message.toLowerCase() : '';
    if (error.status !== 409 || !message) {
      return false;
    }

    let handled = false;

    if (message.includes('username')) {
      this.setServerFieldError(this.username, 'duplicateTaken');
      this.username?.markAsTouched();
      handled = true;
    }

    if (message.includes('email')) {
      this.setServerFieldError(this.email, 'duplicateTaken');
      this.email?.markAsTouched();
      handled = true;
    }

    if (handled) {
      this.errorMessage = '';
    }

    return handled;
  }

  private clearRegistrationConflictErrors(): void {
    this.clearServerFieldError(this.username, 'duplicateTaken');
    this.clearServerFieldError(this.email, 'duplicateTaken');
  }

  private setServerFieldError(control: AbstractControl | null, errorKey: string): void {
    if (!control) {
      return;
    }

    control.setErrors({ ...(control.errors ?? {}), [errorKey]: true });
  }

  private clearServerFieldError(control: AbstractControl | null, errorKey: string): void {
    if (!control?.errors?.[errorKey]) {
      return;
    }

    const { [errorKey]: _, ...remainingErrors } = control.errors;
    control.setErrors(Object.keys(remainingErrors).length > 0 ? remainingErrors : null);
  }

  get username() {
    return this.registerForm.get('username');
  }

  get email() {
    return this.registerForm.get('email');
  }

  get password() {
    return this.registerForm.get('password');
  }

  get confirmPassword() {
    return this.registerForm.get('confirmPassword');
  }
}
