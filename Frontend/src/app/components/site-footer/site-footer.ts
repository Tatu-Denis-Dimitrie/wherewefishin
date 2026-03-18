import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-site-footer',
  imports: [CommonModule],
  templateUrl: './site-footer.html',
  styleUrl: './site-footer.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SiteFooter {
  readonly currentYear = new Date().getFullYear();
}
