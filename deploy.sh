#!/bin/bash

# Production Deployment Script for ACOOMH Backend
# This script handles the complete deployment process including database migrations

set -e  # Exit on any error

echo "🚀 Starting ACOOMH Backend Production Deployment..."

# Configuration
APP_NAME="acoomh-backend"
DB_BACKUP_PATH="/backups/mysql"
UPLOAD_PATH="/var/www/uploads"

# Create necessary directories
echo "📁 Creating upload directories..."
sudo mkdir -p $UPLOAD_PATH/locations/photos
sudo mkdir -p $UPLOAD_PATH/locations/menus
sudo chmod -R 755 $UPLOAD_PATH
sudo chown -R www-data:www-data $UPLOAD_PATH

# Backup existing database
echo "💾 Creating database backup..."
sudo mkdir -p $DB_BACKUP_PATH
BACKUP_FILE="$DB_BACKUP_PATH/backup-$(date +%Y%m%d-%H%M%S).sql"
sudo docker exec mysql-container mysqldump -u root -p$MYSQL_ROOT_PASSWORD $MYSQL_DATABASE > $BACKUP_FILE
echo "Database backed up to: $BACKUP_FILE"

# Stop existing services
echo "🛑 Stopping existing services..."
sudo docker-compose down

# Build new Docker image
echo "🔨 Building new Docker image..."
sudo docker build -t $APP_NAME .

# Apply database migrations (manual SQL)
echo "🗄️  Applying database migrations..."
echo "Please run the following SQL script manually against your database:"
echo "File: sql-migrations/add-file-paths.sql"
echo "Press Enter when database migration is complete..."
read -p ""

# Start services
echo "🚀 Starting services with new image..."
sudo docker-compose -f docker-compose.prod.yml up -d

# Wait for services to be ready
echo "⏳ Waiting for services to start..."
sleep 30

# Health check
echo "🔍 Performing health check..."
if curl -f http://localhost:8080/health; then
    echo "✅ Backend is running successfully!"
    
    # Test file upload functionality
    echo "🧪 Testing file upload functionality..."
    if curl -f http://localhost:8080/api/test/upload-ready; then
        echo "✅ File upload system is ready!"
    else
        echo "⚠️  File upload system may need attention"
    fi
    
    echo "🎉 Deployment completed successfully!"
    echo ""
    echo "📊 Service Status:"
    sudo docker-compose -f docker-compose.prod.yml ps
    
    echo ""
    echo "📁 Upload directories created:"
    echo "  - Photos: $UPLOAD_PATH/locations/photos"
    echo "  - Menus: $UPLOAD_PATH/locations/menus"
    
    echo ""
    echo "🔗 Application URLs:"
    echo "  - Backend API: http://your-domain.com/api"
    echo "  - Health Check: http://your-domain.com/health"
    
else
    echo "❌ Health check failed! Please check the logs:"
    echo "Backend logs:"
    sudo docker-compose -f docker-compose.prod.yml logs acoomh-backend
    exit 1
fi
