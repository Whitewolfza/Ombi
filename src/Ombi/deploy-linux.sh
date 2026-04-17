#!/bin/bash
# Ombi Linux Deployment Script
# Run this script on your Linux server after copying the published files

set -e

OMBI_DIR="/opt/Ombi"
OMBI_USER="ombi"
SERVICE_FILE="/etc/systemd/system/ombi.service"

echo "=============================================="
echo "Ombi Linux Deployment Script"
echo "=============================================="

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo "Please run as root (use sudo)"
    exit 1
fi

# Create ombi user if it doesn't exist
if ! id "$OMBI_USER" &>/dev/null; then
    echo "Creating ombi user..."
    useradd -r -s /bin/false $OMBI_USER
fi

# Create directory structure
echo "Creating directory structure..."
mkdir -p $OMBI_DIR
mkdir -p /etc/ombi

# Copy files (assumes this script is run from the publish directory)
echo "Copying files to $OMBI_DIR..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cp -r "$SCRIPT_DIR"/* $OMBI_DIR/

# Set permissions
echo "Setting permissions..."
chown -R $OMBI_USER:$OMBI_USER $OMBI_DIR
chown -R $OMBI_USER:$OMBI_USER /etc/ombi
chmod +x $OMBI_DIR/Ombi

# Create systemd service
echo "Creating systemd service..."
cat > $SERVICE_FILE << 'EOF'
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
EOF

# Reload systemd
echo "Reloading systemd..."
systemctl daemon-reload

# Enable and start service
echo "Enabling Ombi service..."
systemctl enable ombi

echo ""
echo "=============================================="
echo "Deployment complete!"
echo "=============================================="
echo ""
echo "To start Ombi:"
echo "  sudo systemctl start ombi"
echo ""
echo "To check status:"
echo "  sudo systemctl status ombi"
echo ""
echo "To view logs:"
echo "  sudo journalctl -u ombi -f"
echo ""
echo "Ombi will be available at: http://your-server-ip:5000"
echo "=============================================="
