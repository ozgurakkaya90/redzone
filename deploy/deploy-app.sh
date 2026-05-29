#!/bin/bash
# Deploy via EC2 Instance Connect (SSH) + S3
# SSM yerine SSH kullanılır: daha güvenilir, JSON escaping sorunu yok.
set -e

INSTANCE_ID="i-03b45448b8fdf2573"
REGION="eu-central-1"
S3_BUCKET="risk-mgmt-deploy-1777442395"
EC2_USER="ec2-user"
EC2_IP="63.184.112.96"
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TEMP_KEY="/tmp/redzone-deploy-key"

# ─── Gerekli değişken kontrolü ───────────────────────────────────────────────
if [ -z "$DB_PASSWORD" ] || [ -z "$JWT_SECRET" ] || \
   [ -z "$AWS_ACCESS_KEY_ID" ] || [ -z "$AWS_SECRET_ACCESS_KEY" ]; then
    echo "HATA: Gerekli çevre değişkenleri eksik!"
    echo "  export AWS_ACCESS_KEY_ID='AKIA...'"
    echo "  export AWS_SECRET_ACCESS_KEY='xyz...'"
    echo "  export DB_PASSWORD='...'"
    echo "  export JWT_SECRET='...'"
    exit 1
fi

if ! aws sts get-caller-identity &>/dev/null; then
    echo "HATA: AWS kimlik bilgileri geçersiz."
    exit 1
fi

RDS=${DB_HOST:-"risk-management-db.cbyq4agakceq.eu-central-1.rds.amazonaws.com"}
CONN_STR="Server=${RDS};Database=RiskManagement;User=riskapp;Password=${DB_PASSWORD};SslMode=Required;"

echo "=== Risk Management Deploy ==="
echo "Instance : $INSTANCE_ID"
echo "Elastic IP: $EC2_IP"
echo ""

# ─── 1. Geçici SSH anahtarı ───────────────────────────────────────────────────
echo "[1/5] SSH anahtarı hazırlanıyor..."
rm -f "$TEMP_KEY" "${TEMP_KEY}.pub"
ssh-keygen -t ed25519 -f "$TEMP_KEY" -N "" -q
aws ec2-instance-connect send-ssh-public-key \
    --instance-id "$INSTANCE_ID" \
    --instance-os-user "$EC2_USER" \
    --ssh-public-key "$(cat ${TEMP_KEY}.pub)" \
    --region "$REGION" \
    --query 'Success' --output text | grep -q "True" || { echo "HATA: SSH key yüklenemedi"; exit 1; }
echo "  SSH hazır (60s geçerli)"

SSH="ssh -i $TEMP_KEY -o StrictHostKeyChecking=no -o ConnectTimeout=15 -o BatchMode=yes $EC2_USER@$EC2_IP"

# ─── 2. Disk kontrolü ve temizlik ────────────────────────────────────────────
echo "[2/5] Disk kontrolü..."
DISK_PCT=$($SSH "df / | tail -1 | awk '{print \$5}' | tr -d '%'" 2>/dev/null)
echo "  Disk kullanımı: ${DISK_PCT}%"

if [ "${DISK_PCT:-0}" -gt 70 ]; then
    echo "  ⚠ Disk %${DISK_PCT} dolu — otomatik temizlik başlıyor..."

    # SSH key'i yenile (temizlik sürebilir)
    aws ec2-instance-connect send-ssh-public-key \
        --instance-id "$INSTANCE_ID" --instance-os-user "$EC2_USER" \
        --ssh-public-key "$(cat ${TEMP_KEY}.pub)" --region "$REGION" \
        --query 'Success' --output text &>/dev/null

    $SSH "
        sudo docker image prune -af 2>/dev/null && echo '  - Kullanılmayan Docker image temizlendi'
        sudo docker builder prune -af 2>/dev/null && echo '  - Docker build cache temizlendi'
        sudo rm -rf /tmp/app-build /tmp/app.tar.gz 2>/dev/null && echo '  - Eski build dosyaları silindi'
        sudo journalctl --vacuum-size=50M 2>/dev/null && echo '  - Journal loglar sıkıştırıldı'
        echo \"  Disk sonrası: \$(df / | tail -1 | awk '{print \$5}') kullanımda\"
    " 2>&1

    # Temizlikten sonra key yenile
    aws ec2-instance-connect send-ssh-public-key \
        --instance-id "$INSTANCE_ID" --instance-os-user "$EC2_USER" \
        --ssh-public-key "$(cat ${TEMP_KEY}.pub)" --region "$REGION" \
        --query 'Success' --output text &>/dev/null
else
    echo "  Disk yeterli, temizlik gerekmez."
fi

# ─── 3. Kod paketleniyor ─────────────────────────────────────────────────────
echo "[3/5] Kod paketleniyor..."
cd "$PROJECT_ROOT"
tar --exclude='.DS_Store' --exclude='._*' \
    --exclude='./.git' \
    --exclude='./RiskManagement/bin' \
    --exclude='./RiskManagement/obj' \
    --exclude='./RiskManagement/StrykerOutput' \
    --exclude='./RiskManagement/uploads' \
    --exclude='*.db' --exclude='./deploy' \
    -czf /tmp/rm-app.tar.gz .
echo "  Paket boyutu: $(du -sh /tmp/rm-app.tar.gz | cut -f1)"

echo "[3/5] S3'e yükleniyor..."
aws s3 cp /tmp/rm-app.tar.gz "s3://${S3_BUCKET}/app.tar.gz" --region "$REGION"
echo "  s3://${S3_BUCKET}/app.tar.gz"

# ─── 4. EC2'ya deploy script'i oluştur ve S3'e yükle ────────────────────────
echo "[4/5] Deploy script hazırlanıyor..."
cat > /tmp/ec2-deploy.sh << SCRIPT_EOF
#!/bin/bash
set -e
exec > /tmp/deploy-output.log 2>&1

echo "[EC2-1] Paket indiriliyor..."
aws s3 cp s3://${S3_BUCKET}/app.tar.gz /tmp/app.tar.gz --region ${REGION}

echo "[EC2-2] Paket açılıyor..."
rm -rf /tmp/app-build && mkdir -p /tmp/app-build
tar xzf /tmp/app.tar.gz -C /tmp/app-build
find /tmp/app-build -name '._*' -delete

echo "[EC2-3] Docker build..."
cd /tmp/app-build
docker build --no-cache -t risk-management:latest .

echo "[EC2-4] Container yeniden başlatılıyor..."
docker stop risk-management 2>/dev/null || true
docker rm   risk-management 2>/dev/null || true
SCRIPT_EOF

# docker run satırını ayrıca yaz (bağlantı string özel karakterler içerebilir)
cat >> /tmp/ec2-deploy.sh << SCRIPT_EOF2
docker run -d --name risk-management --restart unless-stopped \\
  -p 127.0.0.1:8080:8080 \\
  -v risk-uploads:/app/uploads \\
  -v risk-logo-uploads:/app/wwwroot/uploads \\
  -v risk-dp-keys:/root/.aspnet/DataProtection-Keys \\
  -e ASPNETCORE_ENVIRONMENT=Production \\
  -e "ConnectionStrings__DefaultConnection=${CONN_STR}" \\
  -e "Jwt__Key=${JWT_SECRET}" \\
  -e AppSettings__UseSqlite=false \\
  -e AppSettings__DemoMode=false \\
  -e AppSettings__DemoReset=true \\
  risk-management:latest

echo "[EC2-5] Container durumu:"
sleep 15 && docker ps | grep risk-management

echo "[EC2-6] Temizlik..."
rm -rf /tmp/app-build /tmp/app.tar.gz
docker image prune -f 2>/dev/null || true
echo "[EC2] Deploy tamamlandı."
SCRIPT_EOF2

chmod +x /tmp/ec2-deploy.sh
aws s3 cp /tmp/ec2-deploy.sh "s3://${S3_BUCKET}/deploy.sh" --region "$REGION"

# ─── 5. EC2'da çalıştır ──────────────────────────────────────────────────────
echo "[5/5] EC2'da build & deploy başlıyor (3-5 dk)..."

# Key yenile (build uzun sürer)
aws ec2-instance-connect send-ssh-public-key \
    --instance-id "$INSTANCE_ID" --instance-os-user "$EC2_USER" \
    --ssh-public-key "$(cat ${TEMP_KEY}.pub)" --region "$REGION" \
    --query 'Success' --output text &>/dev/null

$SSH "
    sudo aws s3 cp s3://${S3_BUCKET}/deploy.sh /tmp/deploy.sh --region ${REGION}
    sudo chmod +x /tmp/deploy.sh
    sudo bash /tmp/deploy.sh
    echo '--- Deploy Logu ---'
    sudo cat /tmp/deploy-output.log
" 2>&1

# Temizlik
rm -f "$TEMP_KEY" "${TEMP_KEY}.pub"

# ─── Sağlık kontrolü ─────────────────────────────────────────────────────────
echo ""
echo "Sağlık kontrolü (30s bekleniyor, migration sürebilir)..."
sleep 30
HTTP=$(curl -s -o /dev/null -w "%{http_code}" https://redzone.ozgurakkaya.net/healthz 2>/dev/null)
if [ "$HTTP" = "200" ]; then
    echo "✓ Uygulama sağlıklı (HTTP $HTTP)"
else
    echo "✗ Sağlık kontrolü başarısız (HTTP $HTTP)"
    exit 1
fi

echo ""
echo "=== DEPLOY TAMAMLANDI ==="
echo "URL: https://redzone.ozgurakkaya.net"
