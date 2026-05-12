import { Booking } from '../../models/booking.model';

export interface BookingQrPayload {
  bookingId: number;
  token: string;
  spot?: string;
  user?: string;
}

type BookingQrSource = Pick<Booking, 'id' | 'verificationToken' | 'fishingSpotName'>;

export function buildBookingQrPayload(booking: BookingQrSource, username?: string | null): string {
  return JSON.stringify({
    bookingId: booking.id,
    token: booking.verificationToken ?? '',
    spot: booking.fishingSpotName,
    user: username ?? ''
  } satisfies BookingQrPayload);
}

export function parseBookingQrPayload(decodedText: string): BookingQrPayload | null {
  try {
    const parsed = JSON.parse(decodedText) as Partial<BookingQrPayload> & { verificationToken?: string };
    const bookingId = parsed.bookingId;
    const token = typeof parsed.token === 'string'
      ? parsed.token
      : typeof parsed.verificationToken === 'string'
        ? parsed.verificationToken
        : null;

    if (typeof bookingId !== 'number' || !Number.isInteger(bookingId) || bookingId <= 0 || !token?.trim()) {
      return null;
    }

    return {
      bookingId,
      token: token.trim(),
      spot: typeof parsed.spot === 'string' ? parsed.spot : undefined,
      user: typeof parsed.user === 'string' ? parsed.user : undefined
    };
  } catch {
    return null;
  }
}