# WhereWeFishin — Backend

<p>
  <img alt=".NET" src="https://img.shields.io/badge/.NET_9-512BD4?style=for-the-badge&logo=dotnet&logoColor=white"/>
  <img alt="CSharp" src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white"/>
  <img alt="EF Core" src="https://img.shields.io/badge/EF_Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white"/>
  <img alt="SQL Server" src="https://img.shields.io/badge/SQL_Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white"/>
  <img alt="xUnit" src="https://img.shields.io/badge/xUnit-512BD4?style=for-the-badge&logo=dotnet&logoColor=white"/>
</p>

.NET 9 REST API for the WhereWeFishin platform. Handles authentication, business logic, data persistence, Stripe payments, email delivery, and proxying of video/image uploads to the Python microservice.

---

## Table of Contents

- [Solution Structure](#solution-structure)
- [API Reference](#api-reference)
- [Architecture & Patterns](#architecture--patterns)
- [Authentication & Authorization](#authentication--authorization)
- [Database](#database)
- [Email](#email)
- [Video & Image Analysis](#video--image-analysis)
- [Commands](#commands)
- [Testing](#testing)

---

## Solution Structure

```
backend/
├── WhereWeFishin.API/
│   ├── Controllers/
│   │   ├── Admin/                  # AdminController
│   │   ├── Auth/                   # AuthController
│   │   ├── Bookings/               # BookingsController
│   │   ├── Employees/              # EmployeesController
│   │   ├── FishingSpots/           # FishingSpotsController
│   │   ├── ManagerApplications/    # ManagerApplicationsController
│   │   ├── Pontoons/               # PontoonsController
│   │   ├── Reviews/                # ReviewsController
│   │   ├── Stockings/              # StockingsController
│   │   ├── Users/                  # UsersController
│   │   └── VideoAnalysis/          # VideoAnalysisController, ImageAnalysisController
│   ├── Extensions/                 # IServiceCollection extension methods
│   └── Program.cs                  # App bootstrap, middleware pipeline
│
├── WhereWeFishin.Core/
│   ├── DTOs/                       # Request/response DTOs (one folder per domain)
│   ├── Entities/                   # Domain entities (one folder per domain)
│   ├── Enums/                      # Shared enumerations
│   ├── Interfaces/                 # Service and repository contracts
│   └── Services/                   # Business logic implementations
│
├── WhereWeFishin.Database/
│   ├── Configurations/             # EF Core fluent configurations per entity
│   ├── Context/                    # ApplicationDbContext
│   ├── Migrations/                 # EF Core migration history
│   ├── MockData/                   # Seed data for development
│   └── Repositories/               # Repository implementations
│
└── WhereWeFishin.Tests/
    ├── Controllers/                # Controller unit tests
    ├── DTOs/Validation/            # DTO validation tests
    ├── Integration/                # End-to-end API integration tests
    ├── Repositories/               # Repository tests
    ├── Services/                   # Service unit tests
    └── TestHelpers/                # ApiWebApplicationFactory, helpers
```

---

## API Reference

### Auth — `/api/auth`

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/register` | — | Register a new user |
| `POST` | `/login` | — | Login, returns JWT |
| `POST` | `/forgot-password` | — | Send password-reset email |
| `POST` | `/reset-password` | — | Reset password with token |

Rate limited: **5 requests/min** per IP.

### Users — `/api/users`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/{id}` | `User+` | Get user profile |
| `PUT` | `/{id}` | `User+` | Update profile |
| `POST` | `/{id}/avatar` | `User+` | Upload avatar image |
| `PUT` | `/{id}/password` | `User+` | Change password |

### Fishing Spots — `/api/fishingspots`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/` | — | List spots (paginated, filterable) |
| `GET` | `/{id}` | — | Get spot details |
| `POST` | `/` | `Manager+` | Create spot |
| `PUT` | `/{id}` | `Manager+` | Update spot |
| `DELETE` | `/{id}` | `Manager+` | Soft-delete spot |
| `GET` | `/map` | — | Map markers (id, name, lat, lng) |

### Bookings — `/api/bookings`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/` | `User+` | List user's bookings |
| `GET` | `/{id}` | `User+` | Get booking details |
| `POST` | `/` | `User+` | Create booking |
| `POST` | `/payment-intent` | `User+` | Create Stripe PaymentIntent |
| `POST` | `/validate-qr` | `Employee+` | Validate QR check-in token |
| `DELETE` | `/{id}` | `User+` | Cancel booking |

### Reviews — `/api/reviews`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/spot/{spotId}` | — | List reviews for a spot |
| `POST` | `/` | `User+` | Submit a review |
| `DELETE` | `/{id}` | `User+` | Delete own review |

### Pontoons — `/api/pontoons`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/spot/{spotId}` | — | List pontoons for a spot |
| `POST` | `/` | `Manager+` | Add pontoon |
| `PUT` | `/{id}` | `Manager+` | Update pontoon |
| `DELETE` | `/{id}` | `Manager+` | Remove pontoon |

### Employees — `/api/employees`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/spot/{spotId}` | `Manager+` | List employees for a spot |
| `POST` | `/` | `Manager+` | Assign employee to spot |
| `DELETE` | `/{id}` | `Manager+` | Remove employee from spot |

### Stockings — `/api/stockings`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/spot/{spotId}` | — | List stocking events for a spot |
| `POST` | `/` | `Manager+` | Log a stocking event |
| `DELETE` | `/{id}` | `Manager+` | Remove stocking record |

### Manager Applications — `/api/managerapplications`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/` | `Admin` | List all applications |
| `GET` | `/mine` | `User+` | Get own application status |
| `POST` | `/` | `User+` | Submit application |
| `PUT` | `/{id}/approve` | `Admin` | Approve application |
| `PUT` | `/{id}/reject` | `Admin` | Reject application |

### Video Analysis — `/api/videoanalysis`

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/upload` | `User+` | Upload video for analysis |
| `GET` | `/{id}` | `User+` | Poll analysis status and results |
| `GET` | `/` | `User+` | List user's analyses |

Rate limited: **10 requests/min** per IP. Max body: **150 MB**.

### Image Analysis — `/api/imageanalysis`

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/` | `User+` | Classify fish species from image |

### Admin — `/api/admin`

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/users` | `Admin` | List all users |
| `PUT` | `/users/{id}/role` | `Admin` | Change user role |
| `GET` | `/spots` | `Admin` | List all spots |
| `GET` | `/bookings` | `Admin` | List all bookings |
| `GET` | `/stats` | `Admin` | Platform statistics |

---

## Architecture & Patterns

### Layered Architecture

```
HTTP Request
  └─ Controller (WhereWeFishin.API)
       └─ Service interface (WhereWeFishin.Core)
            └─ Service implementation (WhereWeFishin.Core)
                 └─ Repository interface (WhereWeFishin.Core)
                      └─ Repository implementation (WhereWeFishin.Database)
                           └─ ApplicationDbContext → SQL Server
```

Controllers are thin — they validate input, call services, and return HTTP responses. All business logic and ownership checks live in the service layer.

### Soft Delete

`BaseEntity` has an `IsDeleted` flag. A global EF Core query filter on `ApplicationDbContext` automatically excludes soft-deleted rows from every query. Hard deletes are never used for user-facing entities.

### Repository Pattern

A generic `IRepository<T>` provides standard CRUD operations. Domain-specific repositories (e.g., `IFishingSpotRepository`) extend it with custom queries. This keeps controllers and services independent of EF Core.

### Rate Limiting

ASP.NET Core 7+ built-in middleware:
- `/api/auth/*` — 5 requests/minute per IP
- Upload endpoints — 10 requests/minute per IP

---

## Authentication & Authorization

### JWT Flow

1. Client POSTs credentials to `/api/auth/login`
2. Backend verifies password with **BCrypt**, issues a JWT (HS256, 24h expiry)
3. JWT claims: `UserId`, `Username`, `Email`, `Role`
4. Client sends `Authorization: Bearer <token>` on every request
5. Controllers use `[Authorize(Roles = "...")]`; services check resource ownership explicitly

### Role Hierarchy

```
Admin  ⊃  Manager  ⊃  Employee  ⊃  User
```

Each role inherits the permissions of all roles below it.

### Password Reset

1. `POST /api/auth/forgot-password` → generates a time-limited token, sends email with reset link
2. `POST /api/auth/reset-password` → validates token, sets new BCrypt hash

---

## Database

### Applying Migrations

```bash
cd backend

# Add a new migration
dotnet ef migrations add MigrationName \
  --project WhereWeFishin.Database \
  --startup-project WhereWeFishin.API

# Apply to local database
dotnet ef database update \
  --project WhereWeFishin.Database \
  --startup-project WhereWeFishin.API

# Drop and recreate (dev only)
dotnet ef database drop --startup-project WhereWeFishin.API
dotnet ef database update --project WhereWeFishin.Database --startup-project WhereWeFishin.API
```

### Connection String

Development: `backend/WhereWeFishin.API/appsettings.Development.json` (not committed).  
Production: injected via `SQL_SA_PASSWORD` environment variable in `docker-compose.yml`.

---

## Email

Email delivery uses **fire-and-forget SMTP** (Gmail). A failure to send does not roll back the parent operation (e.g., registration still succeeds if the welcome email fails).

Configure in `appsettings.Development.json`:

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

In tests, `IEmailService` is replaced with a no-op stub so no real emails are ever sent.

---

## Video & Image Analysis

The backend acts as an **authenticated proxy** to the Python microservice:

1. Receives the file from the frontend (authenticated, max 150 MB)
2. Creates a `VideoAnalysis` record with `status = Processing`
3. Streams the file to `POST http://python-service:5001/api/analyze-video`
4. On response, updates the record (`status = Completed`, stores results and output path)
5. Frontend polls `GET /api/videoanalysis/{id}` until terminal status

The Python service is **never directly accessible** from outside the Docker network.

---

## Commands

```bash
cd backend

# Run the API (dev)
cd WhereWeFishin.API
dotnet run                    # → http://localhost:5033

# Run all tests
dotnet test

# Run a specific test by name
dotnet test --filter "FullyQualifiedName~AuthIntegrationTests.Register_WithValidPayload"

# Run tests for a specific project
dotnet test WhereWeFishin.Tests

# Build solution
dotnet build
```

---

## Testing

### Strategy

| Layer | Approach |
|---|---|
| DTOs | Validation attribute tests |
| Controllers | Unit tests with mocked services |
| Services | Unit tests with mocked repositories |
| Repositories | Tests against in-memory SQLite |
| Integration | Full HTTP request/response via `ApiWebApplicationFactory` |

### Integration Test Setup

`ApiWebApplicationFactory` replaces:
- **SQL Server** → in-memory SQLite (auto-applies migrations)
- **IEmailService** → no-op stub (never sends real emails)
- **Stripe** → mocked where needed

This means integration tests run with zero external dependencies.

### Running Tests

```bash
cd backend
dotnet test                  # all 349+ tests
dotnet test --verbosity normal
```
