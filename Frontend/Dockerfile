# =============================================
# WhereWeFishin Frontend - Multi-stage build
# =============================================

# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app

# Copy package files for layer caching
COPY package.json package-lock.json* ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build for production
RUN npx ng build --configuration production

# Stage 2: Serve with nginx
FROM nginx:alpine AS runtime

# Install curl for Docker healthcheck
RUN apk add --no-cache curl

# Copy custom nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built Angular app from build stage
# Project name in angular.json is "Frontend" (capital F) → output is dist/Frontend/browser
COPY --from=build /app/dist/Frontend/browser /usr/share/nginx/html

# Expose port
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
