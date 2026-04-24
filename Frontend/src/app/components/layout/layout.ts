import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { CartService } from '../../services/cart.service';
import { FishingSpotService } from '../../services/fishing-spot.service';
import { AppIcon } from '../../shared/icons/app-icon';
import { AppIconName } from '../../shared/icons/app-icon.registry';
import { SiteFooter } from '../site-footer/site-footer';
import { filter, Subscription } from 'rxjs';

type LayoutNavIcon = Extract<AppIconName, 'map' | 'video' | 'profile' | 'qr' | 'admin' | 'manage' | 'cart' | 'bookings' | 'faq'>;

interface LayoutNavItem {
  key: string;
  label: string;
  route: string | readonly (string | number)[];
  icon: LayoutNavIcon;
  exact?: boolean;
  showCartBadge?: boolean;
}

@Component({
  selector: 'app-layout',
  imports: [CommonModule, RouterModule, AppIcon, SiteFooter],
  templateUrl: './layout.html',
  styleUrl: './layout.css'
})
export class Layout implements OnInit, OnDestroy {
  @ViewChild('pageContent') private pageContentRef?: ElementRef<HTMLElement>;

  readonly exactNavMatchOptions = { exact: true } as const;
  readonly inclusiveNavMatchOptions = { exact: false } as const;

  isAdmin = false;
  isEmployee = false;
  isManager = false;
  isAdminRoute = false;
  isSpotManagerRoute = false;
  managerSpotId: number | null = null;
  mobileMenuOpen = false;
  primaryNavItems: LayoutNavItem[] = [];
  secondaryNavItems: LayoutNavItem[] = [];
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
    this.refreshNavigationItems();

    if (this.isManager) {
      this.fishingSpotService.getManaged().subscribe(spots => {
        const mySpot = [...spots].sort((left, right) => left.name.localeCompare(right.name))[0];
        if (mySpot) {
          this.managerSpotId = mySpot.id;
          this.refreshNavigationItems();
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

  private refreshNavigationItems(): void {
    const primaryItems: LayoutNavItem[] = [
      { key: 'home', label: 'Map', route: '/home', icon: 'map', exact: true },
      { key: 'fish-recognition', label: 'Fish Recognition', route: '/fish-recognition', icon: 'video' },
      { key: 'profile', label: 'Profile', route: '/profile', icon: 'profile' },
      ...(this.isEmployee ? [{ key: 'scan-qr', label: 'Scanare QR', route: '/scan-qr', icon: 'qr' as const }] : []),
      ...(this.isAdmin ? [{ key: 'admin', label: 'Admin', route: '/admin', icon: 'admin' as const }] : []),
      ...(this.isManager && this.managerSpotId
        ? [{ key: 'spot-management', label: 'Spot Management', route: ['/spots', this.managerSpotId, 'manage'] as const, icon: 'manage' as const }]
        : []),
      { key: 'cart', label: 'Cart', route: '/cart', icon: 'cart', showCartBadge: true },
      { key: 'my-bookings', label: 'My Bookings', route: '/my-bookings', icon: 'bookings' }
    ];

    this.primaryNavItems = primaryItems;
    this.secondaryNavItems = [
      { key: 'faq', label: 'FAQ', route: '/faq', icon: 'faq' }
    ];
  }

  private resetPageScroll(): void {
    requestAnimationFrame(() => {
      const content = this.pageContentRef?.nativeElement;
      if (content) {
        content.scrollTop = 0;
      }
    });
  }

  private updateRouteState(url: string): void {
    this.isAdminRoute = /^\/admin(?:$|[?#/])/.test(url);
    this.isSpotManagerRoute = /^\/spots\/[^/]+\/manage(?:$|[?#/])/.test(url);
  }
}
