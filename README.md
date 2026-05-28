# WhereWeFishin

<p align="center">
  <img alt="Angular" src="https://img.shields.io/badge/Angular_18-DD0031?style=for-the-badge&logo=angular&logoColor=white"/>
  <img alt=".NET" src="https://img.shields.io/badge/.NET_9-512BD4?style=for-the-badge&logo=dotnet&logoColor=white"/>
  <img alt="Python" src="https://img.shields.io/badge/Python_3.9+-3776AB?style=for-the-badge&logo=python&logoColor=white"/>
  <img alt="SQL Server" src="https://img.shields.io/badge/SQL_Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white"/>
  <img alt="Docker" src="https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white"/>
  <img alt="Stripe" src="https://img.shields.io/badge/Stripe-635BFF?style=for-the-badge&logo=stripe&logoColor=white"/>
</p>

<p align="center">
  <img alt="TypeScript" src="https://img.shields.io/badge/TypeScript-3178C6?style=flat-square&logo=typescript&logoColor=white"/>
  <img alt="CSharp" src="https://img.shields.io/badge/C%23-239120?style=flat-square&logo=csharp&logoColor=white"/>
  <img alt="Leaflet" src="https://img.shields.io/badge/Leaflet.js-199900?style=flat-square&logo=leaflet&logoColor=white"/>
  <img alt="YOLOv8" src="https://img.shields.io/badge/YOLOv8-00FFFF?style=flat-square&logo=yolo&logoColor=black"/>
  <img alt="OpenCV" src="https://img.shields.io/badge/OpenCV-5C3EE8?style=flat-square&logo=opencv&logoColor=white"/>
  <img alt="JWT" src="https://img.shields.io/badge/JWT-000000?style=flat-square&logo=jsonwebtokens&logoColor=white"/>
  <img alt="Nginx" src="https://img.shields.io/badge/Nginx-009639?style=flat-square&logo=nginx&logoColor=white"/>
  <img alt="Cloudflare" src="https://img.shields.io/badge/Cloudflare_Tunnel-F38020?style=flat-square&logo=cloudflare&logoColor=white"/>
</p>

> A full-stack fishing spot discovery and management platform with an interactive satellite map, catch logging, booking system with Stripe payments, and AI-powered fish recognition through video analysis.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Quick Start — VSCode](#quick-start--vscode)
  - [Manual Start](#manual-start)
  - [First-Time Setup](#first-time-setup)
- [Configuration](#configuration)
- [Docker Deployment](#docker-deployment)
- [Services](#services)
  - [Frontend — Angular SPA](#frontend--angular-spa)
  - [Backend — .NET REST API](#backend--net-rest-api)
  - [Fish Recognition — Python Microservice](#fish-recognition--python-microservice)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)

---

## Overview

WhereWeFishin lets anglers discover, review, and book fishing spots on an interactive satellite map. Spot owners and managers can manage reservations, assign employees, and gain insights through AI video analysis — upload a fishing session video and the platform automatically detects and counts unique fish across all frames using YOLOv8 + ByteTrack.

---

## Features

| Feature | Description |
|---|---|
| **Interactive Map** | Satellite imagery via Leaflet.js; draw and save custom fishing zones |
| **Spot Discovery** | Browse, filter, and review fishing locations with detailed spot pages |
| **Booking System** | Reserve pontoons with Stripe-powered checkout; payment intent confirmed before booking is created |
| **Catch Logging** | Log fish catches with species, weight, location, and photos |
| **AI Fish Recognition** | Upload a video — YOLOv8 detects fish, ByteTrack assigns persistent IDs across frames, returns total unique fish count |
| **Image Classification** | Single-frame fish species identification from photo uploads |
| **QR Code Check-in** | Employees scan booking QR codes to validate reservations on-site |
| **User Profiles** | JWT authentication, avatar upload, activity history |
| **Role Management** | Hierarchical roles: `User` → `Employee` → `Manager` → `Admin` |
| **Manager Applications** | Users can apply to become spot managers; admins approve or reject |
| **Stocking Events** | Managers log fish stocking events visible to anglers |
| **Email Notifications** | Welcome email on registration, password-reset links via SMTP |
| **Admin Panel** | Full platform oversight: users, spots, bookings, applications |

---

## Architecture

```
Browser
  └─ Angular SPA (:4200 dev / :80 prod)
       └─ HTTP + JWT Bearer ──► .NET 9 REST API (:5033 dev / :8080 prod)
                                       ├─ EF Core ──────────► SQL Server
                                       └─ HTTP multipart ──► Python Flask (:5001)
                                                                └─ YOLOv8 + ByteTrack + FFmpeg
```

**Key constraint:** The Python microservice is **not publicly reachable** — all video/image uploads are authenticated and proxied through the .NET API.

### Video Analysis Flow

```
1. POST /api/videoanalysis/upload   ← frontend uploads video
2. Backend creates VideoAnalysis record (status = Processing)
3. Backend streams file → Python POST /api/analyze-video
4. Python: YOLOv8 frame detection → ByteTrack ID assignment → FFmpeg re-encode (H.264)
5. Python returns: total fish count, tracked IDs, annotated video path
6. Backend updates record (status = Completed)
7. Frontend polls GET /api/videoanalysis/{id} until terminal status
```

### Authentication & Authorization Flow

```
1. POST /api/auth/login or /register
2. Backend: BCrypt verify → JWT issued (HS256, 24h, claims: UserId/Username/Email/Role)
3. Frontend: token stored in localStorage, sent as Authorization: Bearer <token>
4. Backend: [Authorize(Roles = "...")] + explicit ownership checks in service layer
5. Frontend: route guards (authGuard, role-specific guards) block unauthorized navigation
```

---

## Getting Started

### Prerequisites

| Tool | Version / Notes |
|---|---|
| **Node.js** | 18+ |
| **Angular CLI** | `npm install -g @angular/cli` |
| **.NET SDK** | 9.0 |
| **Python** | 3.9+ |
| **FFmpeg** | `winget install ffmpeg` or `choco install ffmpeg` |
| **SQL Server** | LocalDB or full instance |
| **Docker** *(optional)* | For containerised deployment |

### Quick Start — VSCode

The easiest way to run all three services simultaneously:

**Ctrl+Shift+P** → **Tasks: Run Task** → **Start All Services**

This opens three parallel terminals running the Python service, .NET API, and Angular dev server.

### Manual Start

```powershell
# Terminal 1 — Python Fish Recognition Service
cd fish-recognition-service
.\venv\Scripts\Activate.ps1
python app.py                    # → http://localhost:5001

# Terminal 2 — .NET Backend
cd backend\WhereWeFishin.API
dotnet run                       # → http://localhost:5033

# Terminal 3 — Angular Frontend
cd Frontend
ng serve                         # → http://localhost:4200
```

### First-Time Setup

```powershell
# 1. Place the YOLO model weights
#    Copy best.pt → fish-recognition-service/models/best.pt

# 2. Python virtual environment
cd fish-recognition-service
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt
cd ..

# 3. Frontend dependencies
cd Frontend
npm install
cd ..

# 4. Backend — apply EF Core migrations
cd backend\WhereWeFishin.API
dotnet ef database update --project ../WhereWeFishin.Database
cd ..\..

# 5. Copy and fill in environment variables
cp .env.example .env
```

---

## Configuration

### Environment Variables

Copy `.env.example` to `.env`:

| Variable | Used by | Description |
|---|---|---|
| `SQL_SA_PASSWORD` | SQL Server | SA password for the Docker container |
| `JWT_KEY` | Backend | Secret for signing JWT tokens |
| `SMTP_PASSWORD` | Backend | Gmail app password |
| `STRIPE_SECRET_KEY` | Backend | Server-side Stripe API key |
| `STRIPE_PUBLISHABLE_KEY` | Frontend build | Injected at `ng build` time |
| `CLOUDFLARE_TUNNEL_TOKEN` | Cloudflare | Tunnel authentication token |
| `FRONTEND_URL` | Backend | Base URL for password-reset email links |

Local development secrets live in `backend/WhereWeFishin.API/appsettings.Development.json` (not committed).

### SMTP

```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": "587",
  "EnableSsl": "true",
  "Username": "your-account@gmail.com",
  "Password": "your-app-password",
  "FromEmail": "your-account@gmail.com",
  "FromName": "WhereWeFishin"
}
```

Generate a Gmail App Password at **myaccount.google.com/apppasswords** (requires 2FA).

### Stripe

Set `STRIPE_SECRET_KEY` and `STRIPE_PUBLISHABLE_KEY` in `.env`. The `docker-compose.yml` already maps these to the correct containers. For local dev, also add them to `appsettings.Development.json` and `environment.ts`.

---

## Docker Deployment

```powershell
cp .env.example .env     # fill in all required values
docker compose up --build
```

| Service | Internal Port | Exposed |
|---|---|---|
| Angular (Nginx) | 80 | ✅ via Cloudflare Tunnel |
| .NET API | 8080 | ✅ via Cloudflare Tunnel |
| SQL Server | 1433 | ❌ internal only |
| Python Flask | 5001 | ❌ internal only |

Production traffic is routed through a **Cloudflare Tunnel** — no direct IP exposure required.

---

## Services

### Frontend — Angular SPA

<img alt="Angular" src="https://img.shields.io/badge/Angular_18-DD0031?style=flat-square&logo=angular&logoColor=white"/>
<img alt="TypeScript" src="https://img.shields.io/badge/TypeScript-3178C6?style=flat-square&logo=typescript&logoColor=white"/>
<img alt="Leaflet" src="https://img.shields.io/badge/Leaflet.js-199900?style=flat-square&logo=leaflet&logoColor=white"/>
<img alt="Stripe" src="https://img.shields.io/badge/Stripe_Elements-635BFF?style=flat-square&logo=stripe&logoColor=white"/>

#### Structure

```
Frontend/src/app/
├── components/
│   ├── admin/               # Admin panel (users, spots, bookings overview)
│   ├── auth-shell/          # Login / Register wrapper layout
│   ├── cart/                # Booking cart and checkout
│   ├── faq/                 # Frequently asked questions page
│   ├── fish-recognition/    # Video upload and AI analysis results
│   ├── fishing-spot-detail/ # Spot detail page (map, reviews, pontoons, booking)
│   ├── home/                # Landing page with interactive map
│   ├── image-classification/# Single-image fish species identification
│   ├── layout/              # App shell, navbar
│   ├── login/               # Login form
│   ├── manager-application/ # Apply to become a spot manager
│   ├── my-bookings/         # User booking history and QR codes
│   ├── profile/             # User profile and avatar management
│   ├── qr-scanner/          # Employee QR code scanner for check-in
│   ├── register/            # Registration form
│   ├── site-footer/         # Footer component
│   └── spot-manager/        # Manager dashboard (employees, pontoons, stockings)
├── guards/
│   └── auth.guard.ts        # authGuard + role guards (user/employee/manager/admin)
├── interceptors/            # JWT attachment, error handling
├── models/                  # TypeScript interfaces matching backend DTOs
├── services/                # One service per domain (see below)
└── shared/                  # Reusable icons and QR utilities
```

#### Services

| Service | Responsibility |
|---|---|
| `auth.service.ts` | Login, register, JWT decode, logout |
| `fishing-spot.service.ts` | CRUD for fishing spots, map data |
| `booking.service.ts` | Create/view bookings, Stripe payment intent |
| `cart.service.ts` | Client-side cart state before checkout |
| `video-analysis.service.ts` | Upload video, poll analysis status |
| `review.service.ts` | Submit and fetch spot reviews |
| `pontoon.service.ts` | Pontoon management for managers |
| `employee.service.ts` | Employee assignment per spot |
| `stocking.service.ts` | Fish stocking event logs |
| `user.service.ts` | Profile updates, avatar upload |
| `admin.service.ts` | Admin-level operations |
| `manager-application.service.ts` | Apply/approve/reject manager requests |
| `geocoding.service.ts` | Reverse geocoding for map coordinates |

#### Key Patterns

- **Standalone components** with lazy-loaded routing — no NgModules
- Services use **`shareReplay(1)`** for HTTP request deduplication within a session; cache cleared on logout
- **Stripe Elements** embedded inline — no redirect; `PaymentIntent` confirmed client-side before booking is created on the server
- Route guards chain: `authGuard` (must be logged in) → role guards (`employeeGuard`, `managerGuard`, `adminGuard`)

#### Commands

```bash
cd Frontend
npm install          # install dependencies
ng serve             # dev server → http://localhost:4200
npm run test         # Karma + Jasmine unit tests (opens Chrome)
ng build             # production build → dist/Frontend/browser
```

---

### Backend — .NET REST API

<img alt=".NET" src="https://img.shields.io/badge/.NET_9-512BD4?style=flat-square&logo=dotnet&logoColor=white"/>
<img alt="CSharp" src="https://img.shields.io/badge/C%23-239120?style=flat-square&logo=csharp&logoColor=white"/>
<img alt="EF Core" src="https://img.shields.io/badge/EF_Core-512BD4?style=flat-square&logo=dotnet&logoColor=white"/>
<img alt="xUnit" src="https://img.shields.io/badge/xUnit-5C2D91?style=flat-square&logo=dotnet&logoColor=white"/>

#### Solution Structure

| Project | Responsibility |
|---|---|
| `WhereWeFishin.API` | Controllers, `Program.cs`, middleware, rate limiting |
| `WhereWeFishin.Core` | Entities, DTOs, service interfaces, service implementations |
| `WhereWeFishin.Database` | `ApplicationDbContext`, EF Core migrations, repositories |
| `WhereWeFishin.Tests` | xUnit unit + integration tests (349+ passing) |

#### API Endpoints

| Controller | Base Route | Key Operations |
|---|---|---|
| `AuthController` | `/api/auth` | Register, Login, RefreshToken, ForgotPassword, ResetPassword |
| `UsersController` | `/api/users` | Profile, Avatar, ChangePassword |
| `FishingSpotsController` | `/api/fishingspots` | CRUD spots, list with filters, map data |
| `BookingsController` | `/api/bookings` | Create booking, payment intent, list, QR validation |
| `ReviewsController` | `/api/reviews` | Submit, list, delete reviews per spot |
| `PontoonsController` | `/api/pontoons` | CRUD pontoons per spot |
| `EmployeesController` | `/api/employees` | Assign/remove employees per spot |
| `StockingsController` | `/api/stockings` | Log and list stocking events |
| `ManagerApplicationsController` | `/api/managerapplications` | Apply, list, approve/reject |
| `VideoAnalysisController` | `/api/videoanalysis` | Upload video, poll status, fetch results |
| `ImageAnalysisController` | `/api/imageanalysis` | Single image fish classification |
| `AdminController` | `/api/admin` | Users, spots, bookings, stats overview |

#### Key Patterns

- **Soft delete** — `BaseEntity` has `IsDeleted`; global EF Core query filter excludes soft-deleted rows from all queries automatically
- **Repository pattern** — generic `IRepository<T>` with domain-specific extensions per aggregate
- **Service layer** — business logic and ownership checks in `WhereWeFishin.Core`, never in controllers
- **Rate limiting** — ASP.NET Core middleware: 5 req/min on `/api/auth/*`, 10 req/min on upload endpoints
- **Max request body** — 150 MB (Kestrel config for video uploads)
- **Email** — fire-and-forget SMTP (Gmail); failure does not roll back the parent operation

#### Commands

```bash
cd backend

# Run API
cd WhereWeFishin.API && dotnet run

# Run all tests (349+)
dotnet test

# Run a specific test
dotnet test --filter "FullyQualifiedName~AuthIntegrationTests.Register_WithValidPayload"

# Add EF Core migration
dotnet ef migrations add MigrationName \
  --project WhereWeFishin.Database \
  --startup-project WhereWeFishin.API

# Apply migrations
dotnet ef database update \
  --project WhereWeFishin.Database \
  --startup-project WhereWeFishin.API
```

#### Testing Strategy

Integration tests use `ApiWebApplicationFactory` with **in-memory SQLite** — no external SQL Server required. The email service is replaced with a stub, so tests never send real emails. Tests cover: DTOs/validation, all controllers, services, repositories, and full auth/booking/employee flows.

---

### Fish Recognition — Python Microservice

<img alt="Python" src="https://img.shields.io/badge/Python_3.9+-3776AB?style=flat-square&logo=python&logoColor=white"/>
<img alt="Flask" src="https://img.shields.io/badge/Flask-000000?style=flat-square&logo=flask&logoColor=white"/>
<img alt="YOLOv8" src="https://img.shields.io/badge/YOLOv8-00FFFF?style=flat-square&logo=yolo&logoColor=black"/>
<img alt="OpenCV" src="https://img.shields.io/badge/OpenCV-5C3EE8?style=flat-square&logo=opencv&logoColor=white"/>
<img alt="PyTorch" src="https://img.shields.io/badge/PyTorch-EE4C2C?style=flat-square&logo=pytorch&logoColor=white"/>

A Flask microservice that performs fish detection and tracking on video and image inputs using a custom-trained YOLOv8 model.

#### How It Works

1. Receives a video via `POST /api/analyze-video` (multipart form from the .NET backend)
2. Loads each frame through the YOLOv8 model at 640px resolution
3. ByteTrack assigns a **persistent integer ID** to each fish across frames — re-associating the same individual even after occlusion
4. A colour-coded **trail** is drawn for each tracked fish (configurable length and fade)
5. The annotated video is re-encoded to H.264/AAC via FFmpeg for browser compatibility
6. Returns: total unique fish count, per-ID detection data, path to the annotated output video

#### API Endpoints

| Method | Route | Description |
|---|---|---|
| `GET` | `/health` | Service health check |
| `GET` | `/api/supported-fish` | List of fish species the model recognises |
| `POST` | `/api/analyze-video` | Full video analysis with tracking and annotation |
| `POST` | `/api/analyze-image` | Single-frame fish classification from image upload |
| `DELETE` | `/api/delete-output/<filename>` | Clean up processed output files |
| `GET` | `/outputs/<filename>` | Serve annotated output video |

#### Configuration (top of `app.py`)

```python
TRACK_CONFIDENCE   = 0.69    # YOLO detection threshold (higher = fewer false positives)
USE_FFMPEG_REENCODE = True   # re-encode output for browser compatibility
USE_AV1_CODEC      = False   # True = smaller files, much slower encoding
USE_HALF_PRECISION = True    # FP16 inference (requires CUDA)
IMG_SIZE           = 640     # input resolution
TRACKER_CONFIG     = 'bytetrack.yaml'  # alternative: 'botsort.yaml'

TRAIL_ENABLED      = True    # draw tracking trail per fish
TRAIL_MAX_POINTS   = 35      # trail history length (higher = longer tail)
TRAIL_FADE         = True    # older points become transparent
```

> GPU is used automatically when CUDA is available; falls back to CPU.

#### Model

The trained YOLO weights (`best.pt`) are **not committed** to the repository. Mount the file at `fish-recognition-service/models/best.pt` before starting the service. In Docker, this is handled as a read-only volume mount in `docker-compose.yml`.

#### Commands

```bash
cd fish-recognition-service

# Create and activate virtual environment
python -m venv venv
.\venv\Scripts\Activate.ps1      # Windows
source venv/bin/activate         # Linux/macOS

# Install dependencies
pip install -r requirements.txt
# Note: lapx (ByteTrack) and torch are included

# Run dev server
python app.py                    # → http://localhost:5001

# Test FFmpeg availability
python test_ffmpeg.py
```

---

## Testing

### Backend

```bash
cd backend
dotnet test                      # run all 349+ tests
```

Tests use in-memory SQLite — no SQL Server or external services needed.

### Frontend

```bash
cd Frontend
npm run test                     # Karma + Jasmine, opens Chrome
```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| Python service won't start | Re-create venv: `python -m venv venv && .\venv\Scripts\Activate.ps1 && pip install -r requirements.txt` |
| YOLO model not found | Place `best.pt` in `fish-recognition-service/models/` |
| Database errors on startup | `dotnet ef database drop` then `dotnet ef database update --project ../WhereWeFishin.Database` |
| Frontend won't compile | `cd Frontend && rm -rf node_modules && npm install` |
| FFmpeg not found | `winget install ffmpeg` or `choco install ffmpeg` |
| Stripe errors | Verify `STRIPE_SECRET_KEY` matches the correct Stripe environment (test vs. live) |
| No GPU detected | Service falls back to CPU automatically; expect slower inference |
