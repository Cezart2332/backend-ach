# Production Deployment Script for AcoomH Backend (PowerShell)
# This script sets up the backend with file storage for VPS deployment

param(
    [switch]$SkipBackup = $false,
    [switch]$SkipMigration = $false
)

# Configuration
$ProjectName = "acoomh-backend"
$EnvFile = ".env.prod"
$BackupDir = ".\backups\$(Get-Date -Format 'yyyyMMdd_HHmmss')"

# Colors for output
function Write-Status {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

Write-Status "🚀 Starting AcoomH Backend Production Deployment..."

# Check if environment file exists
if (-not (Test-Path $EnvFile)) {
    Write-Error "Environment file $EnvFile not found!"
    Write-Status "Creating sample environment file..."
    
    $envContent = @"
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
"@
    
    $envContent | Out-File -FilePath $EnvFile -Encoding UTF8
    Write-Warning "Please edit $EnvFile with your production values before continuing!"
    exit 1
}

# Load environment variables
Write-Status "Loading environment variables..."
Get-Content $EnvFile | ForEach-Object {
    if ($_ -match '^([^#]+)=(.+)$') {
        [Environment]::SetEnvironmentVariable($matches[1], $matches[2], "Process")
    }
}

# Validate required environment variables
$mysqlPassword = [Environment]::GetEnvironmentVariable("MYSQL_ROOT_PASSWORD", "Process")
$jwtSecret = [Environment]::GetEnvironmentVariable("JWT_SECRET", "Process")

if ([string]::IsNullOrEmpty($mysqlPassword) -or [string]::IsNullOrEmpty($jwtSecret)) {
    Write-Error "Required environment variables not set in $EnvFile"
    Write-Error "Please set MYSQL_ROOT_PASSWORD and JWT_SECRET"
    exit 1
}

# Check if JWT secret is secure enough
if ($jwtSecret.Length -lt 64) {
    Write-Error "JWT_SECRET must be at least 64 characters long for production security"
    exit 1
}

Write-Status "Environment validated successfully"

# Create necessary directories
Write-Status "Creating directory structure..."
@("nginx\ssl", "database\init", "backups", "logs") | ForEach-Object {
    if (-not (Test-Path $_)) {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
    }
}

# Check if Docker is running
try {
    docker version | Out-Null
} catch {
    Write-Error "Docker is not running or not installed. Please start Docker Desktop."
    exit 1
}

# Check if docker-compose is available
try {
    docker-compose version | Out-Null
} catch {
    Write-Error "docker-compose is not available. Please install Docker Compose."
    exit 1
}

# Backup existing data if deployment exists
if (-not $SkipBackup) {
    $runningContainers = docker-compose -f docker-compose.prod.yml ps --services --filter "status=running" 2>$null
    if ($runningContainers -contains "db") {
        Write-Status "Creating backup of existing deployment..."
        New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
        
        # Backup database
        try {
            docker-compose -f docker-compose.prod.yml exec -T db mysqldump -u root -p"$mysqlPassword" acumh | Out-File -FilePath "$BackupDir\database.sql" -Encoding UTF8
            Write-Status "Database backup created"
        } catch {
            Write-Warning "Could not create database backup: $($_.Exception.Message)"
        }
        
        Write-Status "Backup created at $BackupDir"
    }
}

# Build and deploy
Write-Status "Building application..."
try {
    docker-compose -f docker-compose.prod.yml build
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
} catch {
    Write-Error "Failed to build application: $($_.Exception.Message)"
    exit 1
}

Write-Status "Starting services..."
try {
    docker-compose -f docker-compose.prod.yml up -d
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start services"
    }
} catch {
    Write-Error "Failed to start services: $($_.Exception.Message)"
    exit 1
}

# Wait for services to be ready
Write-Status "Waiting for services to start..."
Start-Sleep -Seconds 10

# Check health
Write-Status "Checking service health..."
$healthCheck = $false
for ($i = 1; $i -le 30; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8080/health" -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Status "✅ API is healthy!"
            $healthCheck = $true
            break
        }
    } catch {
        # Continue trying
    }
    
    Write-Status "Waiting for API to be ready... (attempt $i/30)"
    Start-Sleep -Seconds 2
}

if (-not $healthCheck) {
    Write-Error "❌ API health check failed after 30 attempts"
    Write-Status "Showing container logs..."
    docker-compose -f docker-compose.prod.yml logs api
    exit 1
}

# Run data migration if needed
if (-not $SkipMigration) {
    Write-Status "Checking for pending file migrations..."
    try {
        $migrationResponse = Invoke-WebRequest -Uri "http://localhost:8080/admin/migration/status" -UseBasicParsing
        $migrationData = $migrationResponse.Content | ConvertFrom-Json
        $pendingMigrations = $migrationData.pendingMigrations
        
        if ($pendingMigrations -gt 0) {
            Write-Warning "Found $pendingMigrations locations that need file migration"
            Write-Status "Running file migration..."
            
            $migrationResult = Invoke-WebRequest -Uri "http://localhost:8080/admin/migration/run" -Method POST -UseBasicParsing
            
            if ($migrationResult.StatusCode -eq 200) {
                Write-Status "✅ File migration completed successfully"
            } else {
                Write-Error "❌ File migration failed"
            }
        } else {
            Write-Status "✅ No file migrations needed"
        }
    } catch {
        Write-Warning "Could not check migration status: $($_.Exception.Message)"
    }
}

# Display deployment summary
Write-Status "🎉 Deployment completed successfully!"
Write-Host ""
Write-Host "📋 Deployment Summary:" -ForegroundColor Cyan
Write-Host "  • API URL: http://localhost:8080"
Write-Host "  • Health Check: http://localhost:8080/health"
Write-Host "  • Database: MySQL on localhost:3306"
Write-Host "  • Upload Directory: Managed by Docker volumes"
Write-Host "  • Logs: .\logs\"
Write-Host ""
Write-Host "📁 File Storage Structure:" -ForegroundColor Cyan
Write-Host "  uploads_data (Docker volume)"
Write-Host "  ├── locations/"
Write-Host "  │   ├── {location_id}/"
Write-Host "  │   │   ├── photos/"
Write-Host "  │   │   └── menus/"
Write-Host ""
Write-Host "🔧 Management Commands:" -ForegroundColor Cyan
Write-Host "  • View logs: docker-compose -f docker-compose.prod.yml logs -f"
Write-Host "  • Stop services: docker-compose -f docker-compose.prod.yml down"
Write-Host "  • Restart API: docker-compose -f docker-compose.prod.yml restart api"
Write-Host "  • Database backup: docker-compose -f docker-compose.prod.yml exec db mysqldump -u root -p acumh > backup.sql"
Write-Host ""

# SSL Certificate reminder
if (-not (Test-Path "nginx\ssl\cert.pem") -or -not (Test-Path "nginx\ssl\key.pem")) {
    Write-Warning "SSL certificates not found in nginx\ssl\"
    Write-Warning "For production deployment, please:"
    Write-Warning "1. Obtain SSL certificates for your domain"
    Write-Warning "2. Place cert.pem and key.pem in nginx\ssl\"
    Write-Warning "3. Update nginx configuration if needed"
    Write-Warning "4. Restart nginx: docker-compose -f docker-compose.prod.yml restart nginx"
}

Write-Status "Deployment script completed!"
