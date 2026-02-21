import { Routes } from '@angular/router';
import { Login } from './components/login/login';
import { Register } from './components/register/register';
import { Layout } from './components/layout/layout';
import { Home } from './components/home/home';
import { Profile } from './components/profile/profile';
import { FishRecognition } from './components/fish-recognition/fish-recognition';
import { Admin } from './components/admin/admin';
import { Cart } from './components/cart/cart';
import { authGuard, adminGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  {
    path: '',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: 'home', component: Home },
      { path: 'profile', component: Profile },
      { path: 'fish-recognition', component: FishRecognition },
      { path: 'admin', component: Admin, canActivate: [adminGuard] },
      { path: 'cart', component: Cart }
    ]
  }
];
