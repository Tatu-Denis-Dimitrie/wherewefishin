import { Routes } from '@angular/router';
import { Login } from './components/login/login';
import { Register } from './components/register/register';
import { Home } from './components/home/home';
import { Profile } from './components/profile/profile';
import { FishRecognition } from './components/fish-recognition/fish-recognition';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  { path: 'home', component: Home },
  { path: 'profile', component: Profile },
  { path: 'fish-recognition', component: FishRecognition }
];
