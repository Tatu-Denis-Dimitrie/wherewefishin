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

  private getItemKey(item: CartItem): string {
    return item.pontoonId ? `pontoon-${item.pontoonId}` : `spot-${item.spotId}`;
  }

  addItem(item: CartItem): void {
    const key = this.getItemKey(item);
    const existing = this._items().find(i => this.getItemKey(i) === key);
    if (existing) {
      // Update duration/startDate if already in cart
      this._items.update(items =>
        items.map(i => this.getItemKey(i) === key ? { ...item } : i)
      );
    } else {
      this._items.update(items => [...items, item]);
    }
    this.saveToStorage();
  }

  removeItem(spotId: number, pontoonId?: number): void {
    const key = pontoonId ? `pontoon-${pontoonId}` : `spot-${spotId}`;
    this._items.update(items => items.filter(i => this.getItemKey(i) !== key));
    this.saveToStorage();
  }

  updateItem(spotId: number, patch: Partial<CartItem>, pontoonId?: number): void {
    const key = pontoonId ? `pontoon-${pontoonId}` : `spot-${spotId}`;
    this._items.update(items =>
      items.map(i => this.getItemKey(i) === key ? { ...i, ...patch } : i)
    );
    this.saveToStorage();
  }

  clear(): void {
    this._items.set([]);
    this.saveToStorage();
  }

  isInCart(spotId: number, pontoonId?: number): boolean {
    const key = pontoonId ? `pontoon-${pontoonId}` : `spot-${spotId}`;
    return this._items().some(i => this.getItemKey(i) === key);
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
