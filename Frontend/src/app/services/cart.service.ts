import { Injectable, signal, computed } from '@angular/core';
import { CartItem } from '../models/booking.model';

const CART_KEY = 'wwf_cart';

@Injectable({
  providedIn: 'root'
})
export class CartService {
  private _items = signal<CartItem[]>(this.loadFromStorage());

  readonly items = this._items.asReadonly();
  readonly count = computed(() => this._items().length);
  readonly total = computed(() =>
    this._items().reduce((sum, i) => sum + i.pricePerHour * i.durationHours, 0)
  );

  addItem(item: CartItem): void {
    const existing = this._items().find(i => i.spotId === item.spotId);
    if (existing) {
      // Update duration/startDate if already in cart
      this._items.update(items =>
        items.map(i => i.spotId === item.spotId ? { ...item } : i)
      );
    } else {
      this._items.update(items => [...items, item]);
    }
    this.saveToStorage();
  }

  removeItem(spotId: number): void {
    this._items.update(items => items.filter(i => i.spotId !== spotId));
    this.saveToStorage();
  }

  updateItem(spotId: number, patch: Partial<CartItem>): void {
    this._items.update(items =>
      items.map(i => i.spotId === spotId ? { ...i, ...patch } : i)
    );
    this.saveToStorage();
  }

  clear(): void {
    this._items.set([]);
    this.saveToStorage();
  }

  isInCart(spotId: number): boolean {
    return this._items().some(i => i.spotId === spotId);
  }

  private saveToStorage(): void {
    localStorage.setItem(CART_KEY, JSON.stringify(this._items()));
  }

  private loadFromStorage(): CartItem[] {
    try {
      const raw = localStorage.getItem(CART_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }
}
