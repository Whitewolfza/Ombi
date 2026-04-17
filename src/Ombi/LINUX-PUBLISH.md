# Ombi Linux Publishing Guide

This guide explains how to build and deploy Ombi to Linux servers.

## Prerequisites

### On Windows (Build Machine)
- .NET 8 SDK
- Node.js and npm
- PowerShell

### On Linux (Target Server)
- .NET 8 Runtime (for framework-dependent deployments)
- Or no prerequisites for self-contained deployments

## Publishing Options

### 1. Linux x64 (Framework-Dependent) - Recommended
Smallest package size, requires .NET 8 Runtime on target system.
```powershell
.\Publish-Linux.ps1 -Target linux-x64
```

### 2. Linux ARM64 (Framework-Dependent)
For Raspberry Pi and ARM-based servers.
```powershell
.\Publish-Linux.ps1 -Target linux-arm64
```

### 3. Linux x64 (Self-Contained)
Includes .NET runtime, no prerequisites on target system. Larger package size.
```powershell
.\Publish-Linux.ps1 -Target linux-x64-standalone
```

## Build Options

### Skip Angular Build
If you've already built the Angular app and don't want to rebuild:
```powershell
.\Publish-Linux.ps1 -Target linux-x64 -SkipAngularBuild
```

### Skip Tests
To skip running tests during publish:
```powershell
.\Publish-Linux.ps1 -Target linux-x64 -SkipTests
```

## Manual Publishing

If you prefer to use Visual Studio or command line:

### Using Visual Studio
1. Right-click the Ombi project
2. Select "Publish"
3. Choose one of the Linux profiles:
   - Linux-x64
   - Linux-ARM64
   - Linux-x64-SelfContained

### Using .NET CLI
```bash
# Framework-dependent
dotnet publish -c Release -r linux-x64 --self-contained false

# Self-contained
dotnet publish -c Release -r linux-x64 --self-contained true

# ARM64
dotnet publish -c Release -r linux-arm64 --self-contained false
```

## Deployment to Linux

### Quick Deployment

1. **Copy files to Linux server:**
   ```bash
   scp -r bin/Release/net8.0/publish/linux-x64/* user@server:/tmp/ombi/
   ```

2. **SSH to server and run deployment script:**
   ```bash
   ssh user@server
   cd /tmp/ombi
   chmod +x deploy-linux.sh
   sudo ./deploy-linux.sh
   ```

3. **Start Ombi:**
   ```bash
   sudo systemctl start ombi
   ```

### Manual Deployment

1. **Install .NET Runtime (if not using self-contained):**
   ```bash
   wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   sudo apt update
   sudo apt install dotnet-runtime-8.0
   ```

2. **Create Ombi user:**
   ```bash
   sudo useradd -r -s /bin/false ombi
   ```

3. **Create directories:**
   ```bash
   sudo mkdir -p /opt/Ombi
   sudo mkdir -p /etc/ombi
   ```

4. **Copy files:**
   ```bash
   sudo cp -r /path/to/publish/* /opt/Ombi/
   ```

5. **Set permissions:**
   ```bash
   sudo chown -R ombi:ombi /opt/Ombi
   sudo chown -R ombi:ombi /etc/ombi
   sudo chmod +x /opt/Ombi/Ombi
   ```

6. **Create systemd service:**
   ```bash
   sudo nano /etc/systemd/system/ombi.service
   ```
   
   Paste the following:
   ```ini
   [Unit]
   Description=Ombi - Media Request System
   After=network.target

   [Service]
   Type=simple
   User=ombi
   Group=ombi
   WorkingDirectory=/opt/Ombi
   ExecStart=/opt/Ombi/Ombi --host http://*:5000 --storage /etc/ombi
   Restart=on-failure
   RestartSec=5
   StandardOutput=journal
   StandardError=journal
   SyslogIdentifier=ombi

   [Install]
   WantedBy=multi-user.target
   ```

7. **Enable and start service:**
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable ombi
   sudo systemctl start ombi
   ```

## Configuration

### Database Configuration
Ombi supports SQLite, MySQL, and PostgreSQL. Configure in `/etc/ombi/appsettings.json`.

### Command Line Options
```bash
# Specify custom port
./Ombi --host http://*:3000

# Specify storage path
./Ombi --storage /path/to/storage

# Specify base URL for reverse proxy
./Ombi --baseurl /ombi

# Database migration
./Ombi --migrate
```

## Troubleshooting

### Check Service Status
```bash
sudo systemctl status ombi
```

### View Logs
```bash
# Real-time logs
sudo journalctl -u ombi -f

# All logs
sudo journalctl -u ombi

# Application logs (if configured)
sudo cat /etc/ombi/Logs/log.txt
```

### Permission Issues
```bash
# Fix ownership
sudo chown -R ombi:ombi /opt/Ombi
sudo chown -R ombi:ombi /etc/ombi

# Fix executable permission
sudo chmod +x /opt/Ombi/Ombi
```

### Port Already in Use
```bash
# Find process using port 5000
sudo lsof -i :5000

# Change port in service file
sudo nano /etc/systemd/system/ombi.service
# Update ExecStart line with different port
sudo systemctl daemon-reload
sudo systemctl restart ombi
```

## Reverse Proxy Setup

### Nginx
```nginx
server {
    listen 80;
    server_name ombi.yourdomain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Apache
```apache
<VirtualHost *:80>
    ServerName ombi.yourdomain.com
    
    ProxyPreserveHost On
    ProxyPass / http://localhost:5000/
    ProxyPassReverse / http://localhost:5000/
    
    <Proxy *>
        Order deny,allow
        Allow from all
    </Proxy>
</VirtualHost>
```

## Updating Ombi

1. Stop the service:
   ```bash
   sudo systemctl stop ombi
   ```

2. Backup configuration:
   ```bash
   sudo cp -r /etc/ombi /etc/ombi.backup
   ```

3. Copy new files:
   ```bash
   sudo cp -r /path/to/new/publish/* /opt/Ombi/
   sudo chown -R ombi:ombi /opt/Ombi
   sudo chmod +x /opt/Ombi/Ombi
   ```

4. Start the service:
   ```bash
   sudo systemctl start ombi
   ```

## Support

For issues and questions:
- GitHub: https://github.com/Ombi-app/Ombi
- Discord: https://discord.gg/Sa7wNWb
