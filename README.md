# WhereWeFishin

Fishing platform with interactive map, catch management, and AI-powered fish detection (YOLO).

## Architecture

```
Frontend (Angular 18)  →  Backend (.NET 9 API)  →  SQL Server LocalDB
                                    ↓
                          Python YOLO Microservice
```

**Ports:** Frontend `:4200` | Backend `:5033` | Python Service `:5001`

## Quick Start

### VSCode (Recommended)

Press **Ctrl+Shift+P** → **Tasks: Run Task** → **Start All Services**

This creates 3 terminals in VSCode running Python service, Backend API, and Frontend.

### Manual Start

```powershell
# Terminal 1 - Python Service
cd fish-recognition-service
.\venv\Scripts\Activate.ps1
python app.py

# Terminal 2 - Backend
cd backend\WhereWeFishin.API
dotnet run

# Terminal 3 - Frontend
cd Frontend
ng serve
```

Open http://localhost:4200

## First-Time Setup

```powershell
# 1. Copy YOLO model (if FishTracking folder exists)
Copy-Item "FishTracking\fish_env\runs\detect\runs\detect\fish_detector5\weights\best.pt" -Destination "fish-recognition-service\models\best.pt"

# 2. Python environment
cd fish-recognition-service
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt
cd ..

# 3. Frontend dependencies
cd Frontend
npm install
cd ..

# 4. Database
cd backend\WhereWeFishin.API
dotnet ef database update --project ../WhereWeFishin.Database
cd ../..
```

## Tech Stack

| Layer | Tech |
|-------|------|
| Frontend | Angular 18, TypeScript, Leaflet.js |
| Backend | .NET 9, EF Core, SQL Server LocalDB, JWT |
| AI/ML | Python 3.9+, Flask, YOLOv8, OpenCV, FFmpeg |

## Project Structure

```
WhereWeFishin/
├── Frontend/                     # Angular SPA
├── backend/
│   ├── WhereWeFishin.API/        # REST API
│   ├── WhereWeFishin.Core/       # Business logic, entities, DTOs
│   ├── WhereWeFishin.Database/   # EF Core, migrations, seed data
│   └── WhereWeFishin.Tests/      # Tests
├── fish-recognition-service/     # Python YOLO microservice
│   ├── app.py                    # Flask API
│   ├── models/                   # YOLO model (best.pt)
│   └── requirements.txt
└── .vscode/
    └── tasks.json                # VSCode tasks for running services
```

## Features

- **Interactive Map** — satellite imagery, draw fishing zones, save spots
- **Fish Recognition** — upload video, YOLO detects species with bounding boxes
- **User Profiles** — JWT auth, avatar, activity history
- **Catch Tracking** — log species, weight, location, photos

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Python service won't start | Check venv exists: `cd fish-recognition-service && python -m venv venv && .\venv\Scripts\Activate.ps1 && pip install -r requirements.txt` |
| Model not found | Copy best.pt to `fish-recognition-service/models/` |
| DB errors | `cd backend/WhereWeFishin.API && dotnet ef database drop && dotnet ef database update --project ../WhereWeFishin.Database` |
| Frontend errors | `cd Frontend && rm -rf node_modules && npm install` |