# WhereWeFishin — Frontend

<p>
  <img alt="Angular" src="https://img.shields.io/badge/Angular_18-DD0031?style=for-the-badge&logo=angular&logoColor=white"/>
  <img alt="TypeScript" src="https://img.shields.io/badge/TypeScript-3178C6?style=for-the-badge&logo=typescript&logoColor=white"/>
  <img alt="Leaflet" src="https://img.shields.io/badge/Leaflet.js-199900?style=for-the-badge&logo=leaflet&logoColor=white"/>
  <img alt="Stripe" src="https://img.shields.io/badge/Stripe_Elements-635BFF?style=for-the-badge&logo=stripe&logoColor=white"/>
</p>

Angular 18 single-page application for the WhereWeFishin platform. Communicates exclusively with the .NET backend via JWT-authenticated HTTP requests.

---

## Table of Contents

- [Project Structure](#project-structure)
- [Components](#components)
- [Services](#services)
- [Guards & Interceptors](#guards--interceptors)
- [Key Patterns](#key-patterns)
- [Commands](#commands)
- [Environment Configuration](#environment-configuration)

---

## Project Structure

```
src/app/
├── components/
│   ├── admin/                # Admin panel — users, spots, bookings overview
│   ├── auth-shell/           # Shared layout wrapper for login/register pages
│   ├── cart/                 # Booking cart, Stripe checkout flow
│   ├── faq/                  # FAQ page
│   ├── fish-recognition/     # Video upload UI, polling, results display
│   ├── fishing-spot-detail/  # Spot page: map, reviews, pontoons, booking
│   ├── home/                 # Landing page with interactive Leaflet map
│   ├── image-classification/ # Single-image fish species identification
│   ├── layout/               # App shell and navigation bar
│   ├── login/                # Login form
│   ├── manager-application/  # Form to apply for manager role
│   ├── my-bookings/          # User booking history, QR codes
│   ├── profile/              # Profile details, avatar upload, password change
│   ├── qr-scanner/           # Employee QR code scanner for on-site check-in
│   ├── register/             # Registration form with duplicate detection
│   ├── site-footer/          # Footer
│   └── spot-manager/         # Manager dashboard: employees, pontoons, stockings
├── guards/
│   └── auth.guard.ts         # authGuard + role guards (employee/manager/admin)
├── interceptors/             # JWT attachment, global error handling
├── models/                   # TypeScript interfaces matching backend DTOs
├── services/                 # One service per domain (see below)
└── shared/
    ├── icons/                # SVG icon components
    └── qr/                   # QR code generation utilities
```

---

## Components

### Home & Map
The landing page embeds a **Leaflet.js** satellite map. Users can browse existing fishing spots as markers, click to preview details, and draw custom zones. Map state is preserved via the routing service across navigation.

### Fishing Spot Detail
Full spot page with: tabbed info (description, facilities), embedded mini-map, review section, pontoon availability calendar, and a booking flow that leads directly into the cart.

### Cart & Checkout
Client-side cart accumulates pontoon selections. Checkout creates a Stripe `PaymentIntent` via the backend, embeds **Stripe Elements** inline (no redirect), and only creates the booking record after payment confirmation succeeds.

### Fish Recognition
Upload interface for fishing session videos. After upload, the component **polls** `GET /api/videoanalysis/{id}` every few seconds until the analysis reaches a terminal status (`Completed` or `Failed`). Results display: unique fish count, per-species breakdown, and an inline video player for the annotated output.

### Image Classification
Drag-and-drop or file-picker for a single image. Sends the image to `POST /api/imageanalysis` and renders the top predicted species with confidence scores. Supports zoomed image preview.

### QR Scanner
Uses the device camera to scan booking QR codes. Sends the decoded token to the backend for validation; shows the booking details and check-in status to the employee.

### Spot Manager Dashboard
Role-gated to `Manager`+. Tabs for: managing assigned employees, adding/editing pontoons, logging stocking events, and reviewing bookings for their spots.

### Admin Panel
Role-gated to `Admin`. Full platform oversight: user list with role management, all spots, all bookings, manager application queue.

---

## Services

| Service | Base Route | Responsibility |
|---|---|---|
| `auth.service.ts` | `/api/auth` | Login, register, JWT decode/storage, logout, password reset |
| `fishing-spot.service.ts` | `/api/fishingspots` | CRUD spots, list with filters, map data |
| `booking.service.ts` | `/api/bookings` | Create booking, Stripe payment intent, list, QR |
| `cart.service.ts` | *(local state)* | Client-side cart before checkout |
| `video-analysis.service.ts` | `/api/videoanalysis` | Upload video, poll analysis status, fetch results |
| `review.service.ts` | `/api/reviews` | Submit, fetch, delete reviews |
| `pontoon.service.ts` | `/api/pontoons` | Pontoon CRUD for managers |
| `employee.service.ts` | `/api/employees` | Assign/remove employees per spot |
| `stocking.service.ts` | `/api/stockings` | Log and list fish stocking events |
| `user.service.ts` | `/api/users` | Profile updates, avatar upload |
| `admin.service.ts` | `/api/admin` | Admin-level operations |
| `manager-application.service.ts` | `/api/managerapplications` | Apply, list, approve/reject |
| `geocoding.service.ts` | *(external)* | Reverse geocoding for map coordinates |
| `routing.service.ts` | *(local state)* | Navigation and scroll position management |

All HTTP services use **`shareReplay(1)`** — the first subscriber triggers the request; subsequent subscribers within the same session receive the cached response. Cache is cleared on logout.

---

## Guards & Interceptors

### Guards

All route protection is consolidated in `auth.guard.ts` using Angular's functional guard API:

| Guard | Condition |
|---|---|
| `authGuard` | User must be authenticated (valid JWT) |
| `employeeGuard` | Role must be `Employee`, `Manager`, or `Admin` |
| `managerGuard` | Role must be `Manager` or `Admin` |
| `adminGuard` | Role must be `Admin` |

### Interceptors

- **JWT interceptor** — attaches `Authorization: Bearer <token>` to every outgoing request automatically
- **Error interceptor** — catches 401 responses and redirects to login; surfaces API error messages to the UI

---

## Key Patterns

- **Standalone components** — no NgModules; routes use `loadComponent` for lazy loading
- **Typed observables** — all service methods return `Observable<T>` with explicit generic types
- **Stripe Elements inline** — `PaymentIntent` confirmed client-side; booking created server-side only after confirmation
- **Optimistic conflict detection** — registration checks username/email availability live before submission
- **Image zoom** — classification component supports pinch/click zoom for detailed fish inspection

---

## Commands

```bash
cd Frontend

npm install          # install dependencies (first time)
ng serve             # dev server → http://localhost:4200 (live reload)
npm run test         # unit tests via Karma + Jasmine (opens Chrome)
ng build             # production build → dist/Frontend/browser
ng build --watch     # production build in watch mode
```

---

## Environment Configuration

Development environment lives in `src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5033',
  stripePublishableKey: 'pk_test_...'
};
```

For production builds, `environment.prod.ts` is used. The `STRIPE_PUBLISHABLE_KEY` is injected at Docker build time as a build argument.
