import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AppIconDefinition, AppIconName, getAppIconDefinition } from './app-icon.registry';

const EMPTY_ICON_DEFINITION: AppIconDefinition = {
  viewBox: '0 0 24 24',
  body: ''
};

@Component({
  selector: 'app-icon',
  template: `
    <svg
      xmlns="http://www.w3.org/2000/svg"
      [attr.class]="svgClass || null"
      [attr.width]="width ?? size"
      [attr.height]="height ?? size"
      [attr.viewBox]="definition.viewBox"
      [attr.fill]="fill ?? definition.fill ?? null"
      [attr.stroke]="stroke ?? definition.stroke ?? null"
      [attr.stroke-width]="strokeWidth ?? definition.strokeWidth ?? null"
      [attr.stroke-linecap]="definition.strokeLinecap ?? null"
      [attr.stroke-linejoin]="definition.strokeLinejoin ?? null"
      [attr.aria-hidden]="decorative ? 'true' : null"
      [attr.aria-label]="decorative ? null : (ariaLabel || name)"
      [attr.role]="decorative ? null : 'img'"
      focusable="false"
      [innerHTML]="svgBody">
    </svg>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppIcon {
  definition: AppIconDefinition = EMPTY_ICON_DEFINITION;
  svgBody: SafeHtml | string = '';

  @Input({ required: true })
  set name(value: AppIconName) {
    this.definition = getAppIconDefinition(value);
    this.svgBody = this.sanitizer.bypassSecurityTrustHtml(this.definition.body);
    this._name = value;
  }

  get name(): AppIconName {
    return this._name;
  }

  @Input() size: number | string = 20;
  @Input() width?: number | string;
  @Input() height?: number | string;
  @Input() svgClass = '';
  @Input() strokeWidth?: number | string;
  @Input() fill?: string;
  @Input() stroke?: string;
  @Input() decorative = true;
  @Input() ariaLabel?: string;

  private _name!: AppIconName;

  constructor(private readonly sanitizer: DomSanitizer) {}
}