# WhereWeFishin Backend

ASP.NET Core Web API for the WhereWeFishin application - a platform for managing fishing spots and catches.

## Project Structure

```
backend/
├── WhereWeFishin.API/          # Web API Layer
│   ├── Controllers/            # API Controllers
│   └── Program.cs              # Application startup
├── WhereWeFishin.Core/         # Business Logic Layer
│   ├── Entities/               # Domain entities
│   ├── Interfaces/             # Repository interfaces
│   ├── DTOs/                   # Data Transfer Objects
│   └── Enums/                  # Enumerations
├── WhereWeFishin.Database/     # Data Access Layer
│   ├── Context/                # DbContext
│   ├── Configurations/         # Entity configurations
│   └── Repositories/           # Repository implementations
└── WhereWeFishin.Tests/        # Unit & Integration Tests
```

## Technologies

- **ASP.NET Core 9.0** - Web API Framework
- **Entity Framework Core 9.0** - ORM
- **SQL Server** - Database
- **Swagger/OpenAPI** - API Documentation
- **xUnit** - Testing Framework

## Setup and Installation

### Prerequisites

- .NET 9.0 SDK
- SQL Server or SQL Server LocalDB
- Visual Studio 2022 or VS Code

### Configuration

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd WhereWeFishin/backend
   ```

2. **Configure Connection String**

   Edit `WhereWeFishin.API/appsettings.json` and modify the connection string if needed:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WhereWeFishinDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
     }
   }
   ```

3. **Install EF Core tools (if not already installed)**
   ```bash
   dotnet tool install --global dotnet-ef
   ```

4. **Create and run migrations**
   ```bash
   cd WhereWeFishin.API
   dotnet ef migrations add InitialCreate --project ../WhereWeFishin.Database
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

   The API will be available at:
   - HTTPS: `https://localhost:7xxx`
   - HTTP: `http://localhost:5xxx`
   - Swagger UI: `https://localhost:7xxx/swagger`

## Entities

### User
- Username, Email, Password
- Profile information (FirstName, LastName, ProfilePictureUrl)
- Relationships: FishingSpots, Catches

### FishingSpot
- Name, Description
- Location (Latitude, Longitude)
- Image
- Relationships: User (creator), Catches

### Catch
- FishSpecies, Weight, Length
- CaughtAt (datetime)
- Notes, Image
- Relationships: User, FishingSpot

## API Endpoints

### Users
- `GET /api/users` - Get all users
- `GET /api/users/{id}` - Get a user by ID
- `POST /api/users` - Create a new user
- `PUT /api/users/{id}` - Update a user
- `DELETE /api/users/{id}` - Delete a user

### FishingSpots
- `GET /api/fishingspots` - Get all fishing spots
- `GET /api/fishingspots/{id}` - Get a fishing spot by ID
- `POST /api/fishingspots` - Create a new fishing spot
- `PUT /api/fishingspots/{id}` - Update a fishing spot
- `DELETE /api/fishingspots/{id}` - Delete a fishing spot

### Catches
- `GET /api/catches` - Get all catches
- `GET /api/catches/{id}` - Get a catch by ID
- `POST /api/catches` - Create a new catch
- `PUT /api/catches/{id}` - Update a catch
- `DELETE /api/catches/{id}` - Delete a catch

## Testing

Run the tests:
```bash
cd WhereWeFishin.Tests
dotnet test
```

## Next Steps

The following features can be added:

1. **Authentication & Authorization**
   - JWT Authentication
   - Identity Framework
   - Role-based access control

2. **File Upload**
   - Azure Blob Storage / Local storage
   - Image processing and optimization

3. **Advanced Features**
   - Pagination and filtering
   - Advanced search
   - Statistics and reports
   - Weather API integration
   - Social features (comments, likes, sharing)

4. **Performance**
   - Caching (Redis)
   - Response compression
   - Database indexing optimization

5. **Monitoring & Logging**
   - Application Insights
   - Serilog
   - Health checks

## Contributing

To contribute to this project:
1. Fork the repository
2. Create a branch for your feature
3. Commit your changes
4. Push to the branch
5. Create a Pull Request