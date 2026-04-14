import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { CartService } from '../../services/cart.service';
import { FishingSpotService } from '../../services/fishing-spot.service';
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
  isEmployee = false;
  isManager = false;
  isSpotManagerRoute = false;
  managerSpotId: number | null = null;
  mobileMenuOpen = false;
  private navigationSubscription?: Subscription;

  constructor(
    private authService: AuthService,
    private router: Router,
    public cartService: CartService,
    private fishingSpotService: FishingSpotService
  ) {}

  ngOnInit(): void {
    this.isAdmin = this.authService.isAdmin();
    this.isEmployee = this.authService.isEmployee();
    this.isManager = this.authService.isManagerOrAdmin();
    this.updateRouteState(this.router.url);

    if (this.isManager) {
      const userId = this.authService.getUserId();
      this.fishingSpotService.getAll().subscribe(spots => {
        const mySpot = spots.find(s => s.managerId === userId || s.userId === userId);
        if (mySpot) {
          this.managerSpotId = mySpot.id;
        }
      });
    }

    // Keep a single, predictable scroll container for routed app pages.
    this.navigationSubscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.updateRouteState(event.urlAfterRedirects);
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

  private updateRouteState(url: string): void {
    this.isSpotManagerRoute = /^\/spots\/[^/]+\/manage(?:$|[?#/])/.test(url);
  }
}
