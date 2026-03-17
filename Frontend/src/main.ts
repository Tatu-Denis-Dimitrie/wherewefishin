import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

function isStandalonePwa(): boolean {
  const iosStandalone = (window.navigator as Navigator & { standalone?: boolean }).standalone === true;
  return window.matchMedia('(display-mode: standalone)').matches || iosStandalone;
}

function preventPwaAppZoom(): void {
  if (!isStandalonePwa()) {
    return;
  }

  document.addEventListener('gesturestart', (event: Event) => {
    if (event.cancelable) {
      event.preventDefault();
    }
  }, { passive: false });

  document.addEventListener('gesturechange', (event: Event) => {
    if (event.cancelable) {
      event.preventDefault();
    }
  }, { passive: false });

  document.addEventListener('gestureend', (event: Event) => {
    if (event.cancelable) {
      event.preventDefault();
    }
  }, { passive: false });

  document.addEventListener('touchmove', (event: TouchEvent) => {
    if (event.touches.length > 1 && event.cancelable) {
      event.preventDefault();
    }
  }, { passive: false });

  let lastTouchEnd = 0;
  document.addEventListener('touchend', (event: TouchEvent) => {
    const now = Date.now();
    if (now - lastTouchEnd <= 300 && event.cancelable) {
      event.preventDefault();
    }
    lastTouchEnd = now;
  }, { passive: false });

  window.addEventListener('wheel', (event: WheelEvent) => {
    if (event.ctrlKey && event.cancelable) {
      event.preventDefault();
    }
  }, { passive: false });

  window.addEventListener('keydown', (event: KeyboardEvent) => {
    if (!(event.ctrlKey || event.metaKey)) {
      return;
    }

    if (event.key === '+' || event.key === '-' || event.key === '=' || event.key === '0') {
      event.preventDefault();
    }
  });
}

async function enforcePortraitMode(): Promise<void> {
  if (!isStandalonePwa()) {
    return;
  }

  const orientationApi = screen.orientation as ScreenOrientation & {
    lock?: (orientation: string) => Promise<void>;
  };

  if (!orientationApi.lock) {
    return;
  }

  try {
    await orientationApi.lock('portrait');
  } catch {
    // Ignore lock errors on platforms that restrict this API.
  }
}

bootstrapApplication(App, appConfig)
  .then(() => {
    preventPwaAppZoom();
    void enforcePortraitMode();

    window.addEventListener('orientationchange', () => {
      void enforcePortraitMode();
    });
  })
  .catch((err) => console.error(err));
