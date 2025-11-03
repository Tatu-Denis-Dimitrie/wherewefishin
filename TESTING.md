# Testing WhereWeFishin Application

Quick guide to test the connection between frontend and backend.

## Prerequisites

- .NET 9.0 SDK installed
- Node.js installed
- Two terminal windows

## Step 1: Start the Backend

Open a terminal in the `backend` folder:

```bash
cd backend/WhereWeFishin.API
dotnet run
```

The backend will start on:
- HTTPS: https://localhost:7179
- HTTP: http://localhost:5033

You should see: "Now listening on: https://localhost:7179"

## Step 2: Create Database (First Time Only)

If this is your first time running the backend, create the database:

```bash
# From backend/WhereWeFishin.API folder
dotnet ef migrations add InitialCreate --project ../WhereWeFishin.Database
dotnet ef database update
```

## Step 3: Start the Frontend

Open a **second terminal** in the `Frontend` folder:

```bash
cd Frontend
npm start
```

The frontend will start on http://localhost:4200

## Step 4: Test the Connection

1. Open your browser to http://localhost:4200
2. You should see "WhereWeFishin" header
3. Under "Users from Backend API" you'll see one of:
   - **"Loading users..."** - Frontend is connecting to backend
   - **"No users found..."** - Connection successful! (Database is empty)
   - **"Failed to load users..."** - Backend is not running or CORS issue

## Step 5: Add Test Data

To see actual users, you can:

### Option 1: Use Swagger UI
1. Open https://localhost:7179/swagger
2. Click on "POST /api/users"
3. Click "Try it out"
4. Enter test data:
```json
{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "Password123",
  "firstName": "John",
  "lastName": "Doe"
}
```
5. Click "Execute"
6. Refresh the frontend page

### Option 2: Use curl
```bash
curl -X POST https://localhost:7179/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "jane_doe",
    "email": "jane@example.com",
    "password": "Password123",
    "firstName": "Jane",
    "lastName": "Doe"
  }' \
  -k
```

### Option 3: Use Postman
- URL: `https://localhost:7179/api/users`
- Method: POST
- Body (raw JSON):
```json
{
  "username": "test_user",
  "email": "test@example.com",
  "password": "Password123",
  "firstName": "Test",
  "lastName": "User"
}
```

## Troubleshooting

### Backend won't start
- Make sure .NET 9.0 SDK is installed: `dotnet --version`
- Check if port 7179 is already in use

### Frontend won't start
- Run `npm install` in the Frontend folder
- Check if port 4200 is already in use

### "Failed to load users" error
1. Verify backend is running (check terminal)
2. Verify backend URL in `Frontend/src/app/services/api.service.ts` matches `https://localhost:7179/api`
3. Check browser console for CORS errors
4. Ensure CORS is enabled in backend (it should be by default)

### CORS Error
If you see CORS errors in browser console:
- Backend should have CORS enabled (check `Program.cs`)
- Make sure `UseCors("AllowAll")` is present in the middleware pipeline

## Success!

When everything works, you should see:
- Backend terminal showing API requests
- Frontend displaying user cards with usernames and emails
- Users you create via Swagger/Postman appearing in the frontend

## Next Steps

- Explore other endpoints in Swagger
- Create Fishing Spots and Catches
- Build more frontend components
