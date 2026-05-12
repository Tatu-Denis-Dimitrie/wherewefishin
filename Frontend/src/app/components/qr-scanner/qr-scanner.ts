import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Html5Qrcode } from 'html5-qrcode';
import { EmployeeService } from '../../services/employee.service';
import { QrVerificationResult, SpotEmployee } from '../../models/employee.model';
import { AppIcon } from '../../shared/icons/app-icon';
import { parseBookingQrPayload } from '../../shared/qr/booking-qr';

@Component({
  selector: 'app-qr-scanner',
  imports: [CommonModule, AppIcon],
  templateUrl: './qr-scanner.html',
  styleUrl: './qr-scanner.css'
})
export class QrScanner implements OnInit, OnDestroy {
  scanning = false;
  verifying = false;
  result: QrVerificationResult | null = null;
  errorMessage = '';
  assignedSpots: SpotEmployee[] = [];
  loadingSpots = true;

  private html5Qrcode: Html5Qrcode | null = null;
  private readonly scannerId = 'qr-reader';
  private lastVerifyTime = 0;
  private readonly cooldownMs = 3000;

  constructor(private employeeService: EmployeeService) {}

  ngOnInit(): void {
    this.loadAssignedSpots();
  }

  ngOnDestroy(): void {
    this.stopScanner();
  }

  loadAssignedSpots(): void {
    this.employeeService.getMyAssignedSpots().subscribe({
      next: spots => {
        this.assignedSpots = spots;
        this.loadingSpots = false;
      },
      error: () => {
        this.loadingSpots = false;
      }
    });
  }

  async startScanner(): Promise<void> {
    this.result = null;
    this.errorMessage = '';
    this.lastVerifyTime = 0;

    try {
      this.html5Qrcode = new Html5Qrcode(this.scannerId);
      await this.html5Qrcode.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 260, height: 260 } },
        (decodedText) => this.onQrDetected(decodedText),
        () => {}
      );
      this.scanning = true;
    } catch {
      this.errorMessage = 'Could not access camera. Check permissions.';
    }
  }

  rescan(): void {
    this.startScanner();
  }

  async stopScanner(): Promise<void> {
    if (this.html5Qrcode && this.scanning) {
      try {
        await this.html5Qrcode.stop();
      } catch { /* ignore */ }
      this.html5Qrcode = null;
      this.scanning = false;
    }
  }

  private onQrDetected(decodedText: string): void {
    if (this.verifying) return;
    const now = Date.now();
    if (now - this.lastVerifyTime < this.cooldownMs) return;
    this.lastVerifyTime = now;
    this.verifyQrCode(decodedText);
  }

  private verifyQrCode(decodedText: string): void {
    this.verifying = true;
    this.result = null;
    this.errorMessage = '';

    const data = parseBookingQrPayload(decodedText);
    if (!data) {
      this.errorMessage = 'Invalid QR code format.';
      this.verifying = false;
      this.stopScanner();
      return;
    }

    this.employeeService.verifyQr({
      bookingId: data.bookingId,
      verificationToken: data.token
    }).subscribe({
      next: (res) => {
        this.result = res;
        this.verifying = false;
        this.stopScanner();
      },
      error: () => {
        this.errorMessage = 'Error verifying QR code.';
        this.verifying = false;
        this.stopScanner();
      }
    });
  }

  clearResult(): void {
    this.result = null;
    this.errorMessage = '';
    this.lastVerifyTime = 0;
  }
}
