#!/bin/bash

# Production Deployment Script for AcoomH Backend
# This script sets up the backend with file storage for VPS deployment

set -e  # Exit on any error

echo "🚀 Starting AcoomH Backend Production Deployment..."

# Configuration
PROJECT_NAME="acoomh-backend"
ENV_FILE=".env.prod"
BACKUP_DIR="./backups/$(date +%Y%m%d_%H%M%S)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if environment file exists
if [[ ! -f "$ENV_FILE" ]]; then
    print_error "Environment file $ENV_FILE not found!"
    print_status "Creating sample environment file..."
    
    cat > "$ENV_FILE" << EOF
# Database Configuration
MYSQL_ROOT_PASSWORD=your_secure_password_here

# JWT Configuration (Generate a secure 64+ character key)
JWT_SECRET=your_jwt_secret_key_minimum_64_characters_for_production_security

# File Storage Configuration
UPLOADS_PATH=/var/www/uploads
DOMAIN=api.acoomh.ro

# SSL Configuration (if using custom certificates)
SSL_CERT_PATH=./nginx/ssl/cert.pem
SSL_KEY_PATH=./nginx/ssl/key.pem
EOF

    print_warning "Please edit $ENV_FILE with your production values before continuing!"
    exit 1
fi

# Load environment variables
source "$ENV_FILE"

# Validate required environment variables
if [[ -z "$MYSQL_ROOT_PASSWORD" ]] || [[ -z "$JWT_SECRET" ]]; then
    print_error "Required environment variables not set in $ENV_FILE"
    print_error "Please set MYSQL_ROOT_PASSWORD and JWT_SECRET"
    exit 1
fi

# Check if JWT secret is secure enough
if [[ ${#JWT_SECRET} -lt 64 ]]; then
    print_error "JWT_SECRET must be at least 64 characters long for production security"
    exit 1
fi

print_status "Environment validated successfully"

# Create necessary directories
print_status "Creating directory structure..."
mkdir -p nginx/ssl
mkdir -p database/init
mkdir -p backups
mkdir -p logs

# Set proper permissions for upload directory
print_status "Setting up upload directory permissions..."
sudo mkdir -p /var/www/uploads
sudo chown -R $USER:$USER /var/www/uploads
sudo chmod -R 755 /var/www/uploads

# Backup existing data if deployment exists
if docker-compose -f docker-compose.prod.yml ps | grep -q "Up"; then
    print_status "Creating backup of existing deployment..."
    mkdir -p "$BACKUP_DIR"
    
    # Backup database
    docker-compose -f docker-compose.prod.yml exec -T db mysqldump -u root -p"$MYSQL_ROOT_PASSWORD" acumh > "$BACKUP_DIR/database.sql"
    
    # Backup uploads
    sudo cp -r /var/www/uploads "$BACKUP_DIR/uploads"
    
    print_status "Backup created at $BACKUP_DIR"
fi

# Build and deploy
print_status "Building application..."
docker-compose -f docker-compose.prod.yml build

print_status "Starting services..."
docker-compose -f docker-compose.prod.yml up -d

# Wait for services to be ready
print_status "Waiting for services to start..."
sleep 10

# Check health
print_status "Checking service health..."
for i in {1..30}; do
    if curl -f http://localhost:8080/health > /dev/null 2>&1; then
        print_status "✅ API is healthy!"
        break
    fi
    
    if [[ $i -eq 30 ]]; then
        print_error "❌ API health check failed after 30 attempts"
        print_status "Showing container logs..."
        docker-compose -f docker-compose.prod.yml logs api
        exit 1
    fi
    
    print_status "Waiting for API to be ready... (attempt $i/30)"
    sleep 2
done

# Run data migration if needed
print_status "Checking for pending file migrations..."
MIGRATION_STATUS=$(curl -s http://localhost:8080/admin/migration/status | jq -r '.pendingMigrations // 0')

if [[ "$MIGRATION_STATUS" -gt 0 ]]; then
    print_warning "Found $MIGRATION_STATUS locations that need file migration"
    print_status "Running file migration..."
    
    curl -X POST http://localhost:8080/admin/migration/run
    
    if [[ $? -eq 0 ]]; then
        print_status "✅ File migration completed successfully"
    else
        print_error "❌ File migration failed"
    fi
else
    print_status "✅ No file migrations needed"
fi

# Display deployment summary
print_status "🎉 Deployment completed successfully!"
echo ""
echo "📋 Deployment Summary:"
echo "  • API URL: http://localhost:8080"
echo "  • Health Check: http://localhost:8080/health"
echo "  • Database: MySQL on localhost:3306"
echo "  • Upload Directory: /var/www/uploads"
echo "  • Logs: ./logs/"
echo ""
echo "📁 File Storage Structure:"
echo "  /var/www/uploads/"
echo "  ├── locations/"
echo "  │   ├── {location_id}/"
echo "  │   │   ├── photos/"
echo "  │   │   └── menus/"
echo ""
echo "🔧 Management Commands:"
echo "  • View logs: docker-compose -f docker-compose.prod.yml logs -f"
echo "  • Stop services: docker-compose -f docker-compose.prod.yml down"
echo "  • Restart API: docker-compose -f docker-compose.prod.yml restart api"
echo "  • Database backup: docker-compose -f docker-compose.prod.yml exec db mysqldump -u root -p acumh > backup.sql"
echo ""

# SSL Certificate reminder
if [[ ! -f "nginx/ssl/cert.pem" ]] || [[ ! -f "nginx/ssl/key.pem" ]]; then
    print_warning "SSL certificates not found in nginx/ssl/"
    print_warning "For production deployment, please:"
    print_warning "1. Obtain SSL certificates for your domain"
    print_warning "2. Place cert.pem and key.pem in nginx/ssl/"
    print_warning "3. Update nginx configuration if needed"
    print_warning "4. Restart nginx: docker-compose -f docker-compose.prod.yml restart nginx"
fi

print_status "Deployment script completed!"
