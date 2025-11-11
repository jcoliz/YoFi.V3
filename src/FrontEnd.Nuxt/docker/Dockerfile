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
