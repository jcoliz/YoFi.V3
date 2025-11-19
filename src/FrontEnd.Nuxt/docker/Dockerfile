# Build stage
FROM node:24-alpine AS builder

WORKDIR /app

# Copy package files
# Would be nice to do this as a separate state
#COPY src/FrontEnd.Nuxt/package*.json ./

# Copy source code
COPY ./ ./

# Install dependencies
RUN npm install

# Set the API base URL at build time
ARG NUXT_PUBLIC_API_BASE_URL=https://your-backend.azurewebsites.net
ENV NUXT_PUBLIC_API_BASE_URL=$NUXT_PUBLIC_API_BASE_URL

# Set the solution version at build time
ARG NUXT_PUBLIC_SOLUTION_VERSION=
ENV NUXT_PUBLIC_SOLUTION_VERSION=$NUXT_PUBLIC_SOLUTION_VERSION

# Set the application insights connection string at build time
ARG NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING=
ENV NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING=$NUXT_PUBLIC_APPLICATION_INSIGHTS_CONNECTION_STRING

# Build the static site
RUN npm run generate

# Production stage
FROM nginx:alpine

# Copy the built static files from builder stage
COPY --from=builder /app/.output/public /usr/share/nginx/html

# Copy custom nginx configuration (optional)
COPY docker/nginx.conf /etc/nginx/nginx.conf

# Expose port 80
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
