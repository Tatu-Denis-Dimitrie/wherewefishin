import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { CartService } from '../../services/cart.service';
import { SiteFooter } from '../site-footer/site-footer';
import { filter, Subscription } from 'rxjs';

@Component({
  selector: 'app-layout',
  imports: [CommonModule, RouterModule, SiteFooter],
  templateUrl: './layout.html',
  styleUrl: './layout.css'
})
export class Layout implements OnInit, OnDestroy {
  isAdmin = false;
  mobileMenuOpen = false;
  private navigationSubscription?: Subscription;

  constructor(
    private authService: AuthService,
    private router: Router,
    public cartService: CartService
  ) {}

  ngOnInit(): void {
    this.isAdmin = this.authService.isAdmin();

    // Keep a single, predictable scroll container for routed app pages.
    this.navigationSubscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => {
        this.closeMobileMenu();
        this.resetPageScroll();
      });
  }

  ngOnDestroy(): void {
    this.navigationSubscription?.unsubscribe();
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen = false;
  }

  logout(): void {
    this.authService.logout();
  }

  private resetPageScroll(): void {
    requestAnimationFrame(() => {
      const content = document.querySelector('.page-content');
      if (content instanceof HTMLElement) {
        content.scrollTop = 0;
      }
    });
  }
}
