import { Routes } from '@angular/router';
import { authGuard, adminGuard, managerGuard, employeeGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  {
    path: '',
    loadComponent: () => import('./components/auth-shell/auth-shell').then(m => m.AuthShell),
    children: [
      { path: 'login', loadComponent: () => import('./components/login/login').then(m => m.Login) },
      { path: 'register', loadComponent: () => import('./components/register/register').then(m => m.Register) }
    ]
  },
  {
    path: '',
    loadComponent: () => import('./components/layout/layout').then(m => m.Layout),
    canActivate: [authGuard],
    children: [
      { path: 'home', loadComponent: () => import('./components/home/home').then(m => m.Home) },
      { path: 'profile', loadComponent: () => import('./components/profile/profile').then(m => m.Profile) },
      { path: 'fish-recognition', loadComponent: () => import('./components/fish-recognition/fish-recognition').then(m => m.FishRecognition) },
      { path: 'admin', loadComponent: () => import('./components/admin/admin').then(m => m.Admin), canActivate: [adminGuard] },
      { path: 'cart', loadComponent: () => import('./components/cart/cart').then(m => m.Cart) },
      { path: 'my-bookings', loadComponent: () => import('./components/my-bookings/my-bookings').then(m => m.MyBookings) },
      { path: 'faq', loadComponent: () => import('./components/faq/faq').then(m => m.Faq) },
      { path: 'scan-qr', loadComponent: () => import('./components/qr-scanner/qr-scanner').then(m => m.QrScanner), canActivate: [employeeGuard] },
      { path: 'spots/:id', loadComponent: () => import('./components/fishing-spot-detail/fishing-spot-detail').then(m => m.FishingSpotDetail) },
      { path: 'spots/:id/manage', loadComponent: () => import('./components/spot-manager/spot-manager').then(m => m.SpotManager), canActivate: [managerGuard] }
    ]
  }
];
