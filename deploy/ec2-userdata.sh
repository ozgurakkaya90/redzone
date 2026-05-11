#!/bin/bash
# EC2 instance başlatıldığında otomatik çalışır
set -e

# Docker kur
yum update -y
yum install -y docker git
systemctl start docker
systemctl enable docker
usermod -aG docker ec2-user

# Docker Compose kur
curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose

echo "EC2 hazır. Docker kuruldu."
