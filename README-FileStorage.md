# AcoomH Backend - File Storage Refactor

This backend has been refactored to use file storage on the VPS instead of storing binary data in the database. This improves performance, scalability, and makes it easier to manage photos and menus.

## 🚀 What Changed

### File Storage System
- **Location photos** and **menus** are now stored as files on the VPS
- Each location gets its own folder: `/var/www/uploads/locations/{location_id}/`
- Photos go in: `/var/www/uploads/locations/{location_id}/photos/`
- Menus go in: `/var/www/uploads/locations/{location_id}/menus/`
- Files are served directly via URLs for better performance

### Database Changes
- Added `PhotoPath` and `MenuPath` columns to store file paths
- Legacy binary columns (`Photo`, `MenuData`) are kept for backward compatibility during migration
- Automatic migration system to convert existing binary data to files

### API Changes
- Location endpoints now return file URLs instead of base64 encoded data
- New photo/menu endpoints support both file storage and legacy binary data
- Better error handling and file validation
- Improved performance with reduced database load

## 📁 File Structure

```
/var/www/uploads/
├── locations/
│   ├── 1/
│   │   ├── photos/
│   │   │   └── photos_1692123456_abc123.jpg
│   │   └── menus/
│   │       └── menus_1692123456_def456.pdf
│   ├── 2/
│   │   ├── photos/
│   │   └── menus/
│   └── ...
```

## 🛠️ Production Deployment

### Prerequisites
- Docker and Docker Compose
- At least 10GB free space for file storage
- Domain name configured (for SSL)

### Quick Deploy (Linux/macOS)
```bash
# Make deployment script executable
chmod +x deploy-production.sh

# Run deployment
./deploy-production.sh
```

### Quick Deploy (Windows)
```powershell
# Run deployment
.\deploy-production.ps1
```

### Manual Deployment

1. **Create environment file:**
```bash
cp .env.prod.example .env.prod
# Edit .env.prod with your production values
```

2. **Deploy with Docker Compose:**
```bash
docker-compose -f docker-compose.prod.yml up -d
```

3. **Run file migration (if needed):**
```bash
curl -X POST http://localhost:8080/admin/migration/run
```

## 🔧 Configuration

### Environment Variables (.env.prod)
```env
# Database
MYSQL_ROOT_PASSWORD=your_secure_password

# JWT (must be 64+ characters)
JWT_SECRET=your_very_long_and_secure_jwt_secret_key_for_production

# File Storage
UPLOADS_PATH=/var/www/uploads
DOMAIN=api.acoomh.ro
```

### File Storage Settings (appsettings.json)
```json
{
  "FileStorage": {
    "BasePath": "/var/www/uploads",
    "BaseUrl": "https://api.acoomh.ro/files",
    "MaxFileSize": 10485760,
    "AllowedPhotoTypes": ["image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"],
    "AllowedMenuTypes": ["application/pdf", "image/jpeg", "image/jpg", "image/png"]
  }
}
```

## 📊 Migration System

The backend includes an automatic migration system to convert existing binary data to files:

### Check Migration Status
```bash
curl http://localhost:8080/admin/migration/status
```

### Run Migration
```bash
curl -X POST http://localhost:8080/admin/migration/run
```

## 🔗 API Endpoints

### New File Storage Endpoints
- `GET /locations` - Returns locations with photo/menu URLs
- `GET /locations/{id}` - Returns single location with file URLs
- `GET /locations/{id}/photo` - Returns photo URL or legacy base64
- `POST /companies/{id}/locations` - Creates location with file storage

### Admin Endpoints
- `GET /admin/migration/status` - Check pending migrations
- `POST /admin/migration/run` - Run file migration

### File Access
- Files are served at: `https://api.acoomh.ro/files/locations/{id}/photos/{filename}`
- Static files are served by Nginx for better performance

## 🛡️ Security Features

- **File validation**: Only allowed file types are accepted
- **Secure filenames**: Generated with timestamps and random strings
- **Directory isolation**: Each location has its own folder
- **Rate limiting**: Applied to file uploads and access
- **Access control**: Files are served through controlled endpoints

## 📱 React Native Integration

### Updated Photo Handling
```javascript
// Old way (base64)
<Image source={{uri: `data:image/jpeg;base64,${location.photo}`}} />

// New way (URL)
<Image source={{uri: location.photoUrl}} />
```

### Updated Menu Handling
```javascript
// Old way (download base64)
const downloadMenu = () => {
  // Complex base64 to file conversion
};

// New way (direct URL)
const downloadMenu = () => {
  Linking.openURL(location.menuUrl);
};
```

## 🔧 Maintenance

### View Logs
```bash
docker-compose -f docker-compose.prod.yml logs -f api
```

### Backup Database
```bash
docker-compose -f docker-compose.prod.yml exec db mysqldump -u root -p acumh > backup.sql
```

### Backup Files
```bash
tar -czf uploads-backup-$(date +%Y%m%d).tar.gz /var/www/uploads
```

### Restart Services
```bash
docker-compose -f docker-compose.prod.yml restart api
```

## 🚨 Troubleshooting

### Common Issues

1. **File upload fails**
   - Check disk space: `df -h`
   - Check permissions: `ls -la /var/www/uploads`
   - Check logs: `docker-compose logs api`

2. **Migration fails**
   - Check existing data: `GET /admin/migration/status`
   - Run manually: `POST /admin/migration/run`
   - Check logs for specific errors

3. **Files not accessible**
   - Check Nginx configuration
   - Verify file paths in database
   - Check file permissions

### Health Checks
- API: `http://localhost:8080/health`
- Database: `http://localhost:8080/health/db`
- Migration: `http://localhost:8080/admin/migration/status`

## 📈 Performance Improvements

- **Faster API responses**: No more large binary data in JSON
- **Better caching**: Static files cached by browser and CDN
- **Reduced database size**: Binary data moved to filesystem
- **Concurrent access**: Multiple users can access files simultaneously
- **CDN ready**: Files can be served through CDN for global performance

## 🔄 Backward Compatibility

The system maintains backward compatibility during the migration period:
- Legacy endpoints still work
- Binary data is preserved until migration is complete
- Gradual migration ensures zero downtime
- React Native app works with both old and new data formats

---

For more information or support, check the logs or contact the development team.
