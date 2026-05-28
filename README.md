# WhereWeFishin

A full-stack fishing spot discovery and management platform with an interactive map, catch logging, booking system, and AI-powered fish recognition via video analysis.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Quick Start (VSCode)](#quick-start-vscode)
  - [Manual Start](#manual-start)
  - [First-Time Setup](#first-time-setup)
- [Configuration](#configuration)
  - [Environment Variables](#environment-variables)
  - [SMTP (Email)](#smtp-email)
  - [Stripe (Payments)](#stripe-payments)
- [Docker Deployment](#docker-deployment)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)

---

## Overview

WhereWeFishin lets anglers discover, review, and book fishing spots on an interactive satellite map. Spot owners can manage reservations and gain insights through AI video analysis — upload a fishing session video and the platform automatically tracks and counts unique fish across frames using YOLOv8 and ByteTrack.

---

## Features

| Feature | Description |
|---|---|
| **Interactive Map** | Satellite imagery powered by Leaflet.js; draw and save custom fishing zones |
| **Spot Discovery** | Browse, filter, and review fishing locations with detailed info pages |
| **Booking System** | Reserve spots with Stripe-powered checkout; payment intent confirmed before booking |
| **Catch Logging** | Log catches with species, weight, location, and photos |
| **AI Fish Recognition** | Upload a video — YOLOv8 detects fish per frame, ByteTrack assigns persistent IDs, returns unique fish count |
| **User Profiles** | JWT authentication, avatar upload, activity history |
| **Role System** | Hierarchical roles: `User` → `Employee` → `Manager` → `Admin` |
| **Email Notifications** | Welcome email on registration, password-reset links via SMTP |

---

## Architecture

```
Browser
  └─ Angular SPA (:4200 dev / :80 prod)
       └─ HTTP + JWT Bearer ──► .NET 9 REST API (:5033 dev / :8080 prod)
                                       ├─ EF Core ──► SQL Server
                                       └─ HTTP multipart ──► Python Flask (:5001)
                                                                └─ YOLOv8 + ByteTrack + FFmpeg
```

The Python microservice is **not publicly exposed** — all video uploads are proxied through the .NET API after authentication.

### Video Analysis Flow

1. Frontend uploads video → `POST /api/videoanalysis/upload`
2. Backend creates a `VideoAnalysis` record (`status = Processing`), streams the file to the Python service
3. Python runs YOLOv8 frame-by-frame, tracks fish with ByteTrack, re-encodes output with FFmpeg (H.264)
4. Backend updates the record (`status = Completed`) with results
5. Frontend polls `GET /api/videoanalysis/{id}` until a terminal status is reached

### Authentication Flow

1. Frontend POSTs credentials to `/api/auth/login` or `/api/auth/register`
2. Backend hashes passwords with BCrypt, issues a JWT (HS256, 24h expiry)
3. JWT claims: `UserId`, `Username`, `Email`, `Role`
4. Frontend stores the token in `localStorage`, sends it as `Authorization: Bearer <token>`
5. Backend reads claims from `HttpContext.User`; services enforce resource ownership

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Frontend** | Angular 18, TypeScript, Leaflet.js, Stripe Elements |
| **Backend** | .NET 9, ASP.NET Core, Entity Framework Core, SQL Server |
| **AI / ML** | Python 3.9+, Flask, YOLOv8 (Ultralytics), ByteTrack, OpenCV, FFmpeg |
| **Auth** | JWT Bearer (HS256), BCrypt password hashing |
| **Payments** | Stripe (Payment Intents API) |
| **Infrastructure** | Docker, Docker Compose, Nginx, Cloudflare Tunnel |

---

## Project Structure

```
WhereWeFishin/
├── Frontend/                          # Angular 18 SPA
│   ├── src/app/
│   │   ├── components/                # Shared UI components
│   │   ├── features/                  # Feature modules (map, booking, profile…)
│   │   ├── services/                  # HTTP services (shareReplay caching)
│   │   └── guards/                    # Route guards (auth, role-based)
│   ├── Dockerfile
│   └── nginx.conf
│
├── backend/
│   ├── WhereWeFishin.API/             # Controllers, middleware, Program.cs
│   ├── WhereWeFishin.Core/            # Entities, DTOs, service interfaces & implementations
│   ├── WhereWeFishin.Database/        # ApplicationDbContext, EF migrations, repositories
│   └── WhereWeFishin.Tests/           # xUnit tests (unit + integration, 349+ passing)
│
├── fish-recognition-service/
│   ├── app.py                         # Flask API entry point
│   ├── models/                        # YOLO model weights (best.pt — not in repo)
│   └── requirements.txt
│
├── .github/workflows/
│   ├── ci.yml                         # CI pipeline
│   └── cd.yml                         # CD pipeline
├── docker-compose.yml
└── .env.example
```

---

## Getting Started

### Prerequisites

| Tool | Version |
|---|---|
| Node.js + npm | 18+ |
| Angular CLI | `npm install -g @angular/cli` |
| .NET SDK | 9.0 |
| Python | 3.9+ |
| FFmpeg | Any recent version (`winget install ffmpeg`) |
| SQL Server | LocalDB or full instance |
| Docker (optional) | For containerised deployment |

### Quick Start (VSCode)

The easiest way to run all three services at once:

**Ctrl+Shift+P** → **Tasks: Run Task** → **Start All Services**

This opens three parallel terminals — Python service, .NET API, and Angular dev server.

### Manual Start

```powershell
# Terminal 1 — Python Fish Recognition Service
cd fish-recognition-service
.\venv\Scripts\Activate.ps1
python app.py                    # http://localhost:5001

# Terminal 2 — .NET Backend
cd backend\WhereWeFishin.API
dotnet run                       # http://localhost:5033

# Terminal 3 — Angular Frontend
cd Frontend
ng serve                         # http://localhost:4200
```

Open **http://localhost:4200** in your browser.

### First-Time Setup

```powershell
# 1. Place the YOLO model weights
#    Copy best.pt to fish-recognition-service/models/best.pt

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

# 4. Backend — apply database migrations
cd backend\WhereWeFishin.API
dotnet ef database update --project ../WhereWeFishin.Database
cd ..\..

# 5. Copy and fill in environment variables
cp .env.example .env
```

---

## Configuration

### Environment Variables

Copy `.env.example` to `.env` and fill in the required values:

| Variable | Used by | Description |
|---|---|---|
| `SQL_SA_PASSWORD` | SQL Server | SA account password for the Docker container |
| `JWT_KEY` | Backend | Secret key for signing JWT tokens |
| `SMTP_PASSWORD` | Backend | Gmail app password for outgoing emails |
| `STRIPE_SECRET_KEY` | Backend | Stripe server-side API key |
| `STRIPE_PUBLISHABLE_KEY` | Frontend build | Injected at `ng build` time |
| `CLOUDFLARE_TUNNEL_TOKEN` | Cloudflare | Tunnel authentication token |
| `FRONTEND_URL` | Backend | Base URL used in password-reset email links |

In local development, backend secrets live in `backend/WhereWeFishin.API/appsettings.Development.json` (not committed).

### SMTP (Email)

Add the following to `appsettings.Development.json` or set as environment variables:

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

Generate a Gmail App Password at [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords) (requires 2FA enabled).

### Stripe (Payments)

Set `STRIPE_SECRET_KEY` (server) and `STRIPE_PUBLISHABLE_KEY` (client) in `.env`. The `docker-compose.yml` already maps these to the correct containers. For local development, add them to `appsettings.Development.json` and `environment.ts`.

---

## Docker Deployment

```powershell
# 1. Fill in .env
cp .env.example .env

# 2. Build and start all containers
docker compose up --build
```

Services exposed:
- Frontend → **:80**
- Backend → **:8080** (internal; fronted by Nginx)
- SQL Server → **:1433** (internal)
- Python service → **:5001** (internal only)

Production traffic is routed through a Cloudflare Tunnel — no direct IP exposure required.

---

## Testing

### Backend

```powershell
cd backend

# Run all tests (349+ passing)
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~AuthIntegrationTests.Register_WithValidPayload"
```

Integration tests use `ApiWebApplicationFactory` with an **in-memory SQLite** database — no external SQL Server needed. The email service is stubbed so tests never send real emails.

### Frontend

```powershell
cd Frontend
npm run test        # Karma + Jasmine, opens Chrome
```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| Python service won't start | Re-create the venv: `cd fish-recognition-service && python -m venv venv && .\venv\Scripts\Activate.ps1 && pip install -r requirements.txt` |
| YOLO model not found | Place `best.pt` in `fish-recognition-service/models/` |
| Database errors on startup | Reset: `cd backend/WhereWeFishin.API && dotnet ef database drop && dotnet ef database update --project ../WhereWeFishin.Database` |
| Frontend won't start | Clean install: `cd Frontend && rm -rf node_modules && npm install` |
| FFmpeg not found | Install: `winget install ffmpeg` or via Chocolatey `choco install ffmpeg` |
| Stripe webhook errors | Ensure `STRIPE_SECRET_KEY` is set and the key matches your Stripe dashboard environment |
