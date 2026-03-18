import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-faq',
  imports: [CommonModule],
  templateUrl: './faq.html',
  styleUrl: './faq.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Faq {}
