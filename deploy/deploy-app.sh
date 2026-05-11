#!/bin/bash
# Deploy via AWS SSM + S3 (SSH gerektirmez)
set -e

INSTANCE_ID="i-03b45448b8fdf2573"
REGION="eu-central-1"
S3_BUCKET="risk-mgmt-deploy-1777442395"
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# Güvenlik: Kritik değişkenlerin ve AWS anahtarlarının tanımlandığından emin ol
if [ -z "$DB_PASSWORD" ] || [ -z "$JWT_SECRET" ] || [ -z "$AWS_ACCESS_KEY_ID" ] || [ -z "$AWS_SECRET_ACCESS_KEY" ]; then
    echo "HATA: Gerekli çevre değişkenleri eksik!"
    echo "Lütfen şunları terminalinizde 'export' ederek tanımlayın:"
    echo "  export AWS_ACCESS_KEY_ID='AKIA...'"
    echo "  export AWS_SECRET_ACCESS_KEY='xyz...'"
    echo "  export DB_PASSWORD='...'"
    echo "  export JWT_SECRET='...'"
    exit 1
fi

# ─── 0. Kimlik ve Değişken Kontrolü ──────────────────────────────────────────
# AWS CLI'ın yapılandırılmış olup olmadığını kontrol et
if ! aws sts get-caller-identity &> /dev/null; then
    echo "HATA: AWS kimlik bilgileri bulunamadı!"
    echo "Lütfen 'aws configure' komutunu çalıştırın veya anahtarları export edin."
    exit 1
fi

RDS=${DB_HOST:-"risk-management-db.cbyq4agakceq.eu-central-1.rds.amazonaws.com"}

# Local testler için varsayılanlar yerine sadece env kullanımı
DB_PASSWORD=$DB_PASSWORD
JWT_SECRET=$JWT_SECRET

CONN_STR="Server=${RDS};Database=RiskManagement;User=riskapp;Password=${DB_PASSWORD};SslMode=Required;"

echo "=== Risk Management Deploy ==="
echo "Instance : $INSTANCE_ID"
echo "Elastic IP: 63.184.112.96"
echo ""

# ─── 1. Kodu paketle ────────────────────────────────────────────────────────
echo "[1/4] Kod paketleniyor..."
cd "$PROJECT_ROOT"
tar --exclude='.DS_Store' --exclude='._*' \
    --exclude='./RiskManagement/bin' \
    --exclude='./RiskManagement/obj' \
    --exclude='*.db' --exclude='./deploy' \
    -czf /tmp/rm-app.tar.gz .
echo "  Paket boyutu: $(du -sh /tmp/rm-app.tar.gz | cut -f1)"

# ─── 2. S3'e yükle ──────────────────────────────────────────────────────────
echo "[2/4] S3'e yükleniyor..."
aws s3 cp /tmp/rm-app.tar.gz s3://${S3_BUCKET}/app.tar.gz --region $REGION
echo "  s3://${S3_BUCKET}/app.tar.gz"

# ─── 3. EC2'da build & deploy ───────────────────────────────────────────────
echo "[3/4] EC2'da build ediliyor (SSM)..."
CMD_ID=$(aws ssm send-command \
  --instance-ids "$INSTANCE_ID" \
  --document-name "AWS-RunShellScript" \
  --region "$REGION" \
  --parameters "{\"commands\":[
    \"aws s3 cp s3://${S3_BUCKET}/app.tar.gz /tmp/app.tar.gz --region ${REGION}\",
    \"rm -rf /tmp/app-build && mkdir -p /tmp/app-build\",
    \"tar xzf /tmp/app.tar.gz -C /tmp/app-build\",
    \"find /tmp/app-build -name '._*' -delete\",
    \"cd /tmp/app-build && docker build --no-cache -t risk-management:latest .\",
    \"docker stop risk-management 2>/dev/null || true\",
    \"docker rm risk-management 2>/dev/null || true\",
    \"docker run -d --name risk-management --restart unless-stopped -p 127.0.0.1:8080:8080 -v risk-uploads:/app/uploads -v risk-dp-keys:/root/.aspnet/DataProtection-Keys -e ASPNETCORE_ENVIRONMENT=Production -e ConnectionStrings__DefaultConnection='${CONN_STR}' -e Jwt__Key='${JWT_SECRET}' -e AppSettings__UseSqlite=false -e AppSettings__DemoMode=false risk-management:latest\",
    \"sleep 10 && docker ps | grep risk-management\"
  ]}" \
  --timeout-seconds 600 \
  --query 'Command.CommandId' --output text)

echo "  Komut: $CMD_ID"
echo -n "  Bekleniyor"
while true; do
  RESULT=$(aws ssm get-command-invocation \
    --command-id "$CMD_ID" \
    --instance-id "$INSTANCE_ID" \
    --region "$REGION" \
    --query 'Status' --output text 2>/dev/null)
  if [ "$RESULT" = "Success" ] || [ "$RESULT" = "Failed" ]; then
    echo " $RESULT"
    break
  fi
  echo -n "."
  sleep 10
done

# ─── 4. Sonuç ───────────────────────────────────────────────────────────────
echo "[4/4] Sonuç:"
aws ssm get-command-invocation \
  --command-id "$CMD_ID" \
  --instance-id "$INSTANCE_ID" \
  --region "$REGION" \
  --query 'StandardOutputContent' --output text

if [ "$RESULT" = "Failed" ]; then
  echo "HATA:"
  aws ssm get-command-invocation \
    --command-id "$CMD_ID" \
    --instance-id "$INSTANCE_ID" \
    --region "$REGION" \
    --query 'StandardErrorContent' --output text
  exit 1
fi

echo ""
echo "=== DEPLOY TAMAMLANDI ==="
echo "URL: https://redzone.ozgurakkaya.net"
