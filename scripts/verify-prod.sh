#!/usr/bin/env bash
# Smoke test stack production (CONS-009 — plan mục 6). Chạy sau khi stack đã lên:
#   docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build --wait
#   ./scripts/verify-prod.sh
# Đọc SITE_DOMAIN từ .env.production. Cert tự ký được chấp nhận (curl -k) — chỉ để demo.
set -uo pipefail

cd "$(dirname "$0")/.."
ENV_FILE="${ENV_FILE:-.env.production}"
COMPOSE=(docker compose --env-file "$ENV_FILE" -f docker-compose.prod.yml)
FAILED=0

step() { printf '\n\033[1;34m==> %s\033[0m\n' "$1"; }
ok()   { printf '\033[0;32m  ✓ %s\033[0m\n' "$1"; }
fail() { printf '\033[0;31m  ✗ %s\033[0m\n' "$1"; FAILED=1; }

[ -f "$ENV_FILE" ] || { echo "Không thấy $ENV_FILE — cp .env.production.example $ENV_FILE"; exit 1; }
DOMAIN="$(grep -E '^SITE_DOMAIN=' "$ENV_FILE" | cut -d= -f2-)"
[ -n "$DOMAIN" ] || { echo "SITE_DOMAIN rỗng trong $ENV_FILE"; exit 1; }

# --resolve: không phụ thuộc DNS / file hosts của máy chạy script.
# Các chỗ đọc -w qua `read` có `tr -d '\r'`: curl trên Windows in CRLF.
CURL=(curl -sk --max-time 20 --resolve "$DOMAIN:80:127.0.0.1" --resolve "$DOMAIN:443:127.0.0.1")
BASE="https://$DOMAIN"

step "1. Cấu hình compose"
"${COMPOSE[@]}" config -q && ok "docker-compose.prod.yml hợp lệ" || fail "compose config"

step "2. Image"
for image in culinary-blog/api:prod culinary-blog/frontend:prod; do
  docker image inspect "$image" >/dev/null 2>&1 && ok "$image" || fail "$image chưa build"
done

step "3. HTTP → HTTPS (NFR-SEC-005)"
read -r code location < <("${CURL[@]}" -o /dev/null -w '%{http_code} %{redirect_url}\n' "http://$DOMAIN/recipes" | tr -d '\r')
[ "$code" = "301" ] && [ "$location" = "$BASE/recipes" ] \
  && ok "301 → $location" || fail "http:// trả $code $location"

step "4. /health/ready + HSTS + CSP"
headers="$("${CURL[@]}" -D - -o /dev/null "$BASE/health/ready")"
echo "$headers" | head -n1 | grep -q ' 200' && ok "/health/ready 200" || fail "/health/ready: $(echo "$headers" | head -n1)"
hsts_count="$(echo "$headers" | grep -ci '^strict-transport-security: max-age=31536000')"
[ "$hsts_count" = "1" ] && ok "HSTS max-age=31536000 (đúng 1 header)" || fail "HSTS: thấy $hsts_count header đúng"
# NFR-SEC-004/D68 — đúng MỘT CSP mỗi response: API chặn hết, trang web chỉ cùng nguồn (Nginx không thêm)
csp_api="$(echo "$headers" | grep -i '^content-security-policy:' | tr -d '\r')"
[ "$(echo "$csp_api" | grep -c .)" = "1" ] && echo "$csp_api" | grep -q "default-src 'none'" \
  && ok "CSP API: default-src 'none' (đúng 1 header)" || fail "CSP API: $csp_api"
csp_web="$("${CURL[@]}" -D - -o /dev/null "$BASE/" | grep -i '^content-security-policy:' | tr -d '\r')"
[ "$(echo "$csp_web" | grep -c .)" = "1" ] && echo "$csp_web" | grep -q "default-src 'self'" \
  && echo "$csp_web" | grep -q "frame-ancestors 'none'" \
  && ok "CSP trang web: default-src 'self', frame-ancestors 'none' (đúng 1 header)" || fail "CSP trang web: $csp_web"

step "5. Chỉ nginx publish cổng ra host (D60)"
published="$("${COMPOSE[@]}" ps --format '{{.Service}} {{.Publishers}}' | grep -v '^nginx ' | grep -E 'PublishedPort:[1-9]' || true)"
[ -z "$published" ] && ok "postgres/redis/minio/api/frontend không publish cổng" || fail "service publish cổng: $published"

step "6. Register + login trên DB mới (D59 — migrate + seed role)"
email="verify-$(date +%s)@example.com"
password='Verify@12345'
code="$("${CURL[@]}" -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email\",\"password\":\"$password\",\"displayName\":\"Verify Prod\"}" "$BASE/api/v1/auth/register")"
[ "$code" = "201" ] && ok "register 201" || fail "register trả $code"
code="$("${CURL[@]}" -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email\",\"password\":\"$password\"}" "$BASE/api/v1/auth/login")"
[ "$code" = "200" ] && ok "login 200" || fail "login trả $code"

step "7. Ảnh công khai qua Nginx (D58)"
# Upload qua API cần recipe (FR-RCP-003, chưa có) → ghi thẳng ảnh PNG 1×1 vào bucket để kiểm tra đường đọc.
# D66 — service minio-init là amazon/aws-cli (entrypoint sh -c), dùng lại để ghi/xóa ảnh thử.
s3_run() {
  "${COMPOSE[@]}" run --rm -T minio-init "aws --endpoint-url http://minio:9000 $1" >/dev/null 2>&1
}
key="verify/dot-$(date +%s).png"
png='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=='
if "${COMPOSE[@]}" run --rm -T minio-init \
     "echo $png | base64 -d | aws --endpoint-url http://minio:9000 s3 cp - s3://culinary-blog/$key --content-type image/png" \
     >/dev/null 2>&1; then
  code="$("${CURL[@]}" -o /dev/null -w '%{http_code}' "$BASE/culinary-blog/$key")"
  [ "$code" = "200" ] && ok "GET /culinary-blog/$key 200" || fail "GET ảnh công khai trả $code"
  code="$("${CURL[@]}" -o /dev/null -w '%{http_code}' -X PUT --data x "$BASE/culinary-blog/$key")"
  [ "$code" = "403" ] && ok "PUT qua Nginx bị chặn (403)" || fail "PUT trả $code (phải 403)"
  # Policy chỉ cho s3:GetObject — không lộ danh sách file của bucket
  code="$("${CURL[@]}" -o /dev/null -w '%{http_code}' "$BASE/culinary-blog/")"
  [ "$code" = "403" ] && ok "LIST bucket ẩn danh bị chặn (403)" || fail "LIST bucket trả $code (phải 403)"
  # Server Next.js tải ảnh qua https://$DOMAIN → alias nginx + NODE_EXTRA_CA_CERTS + dangerouslyAllowLocalIP.
  image_url="$(printf '%s' "$BASE/culinary-blog/$key" | sed 's#:#%3A#g; s#/#%2F#g')"
  read -r code ctype < <("${CURL[@]}" -o /dev/null -w '%{http_code} %{content_type}\n' \
    "$BASE/_next/image?url=$image_url&w=64&q=75" | tr -d '\r')
  [ "$code" = "200" ] && [[ "$ctype" == image/* ]] && ok "next/image tối ưu được ảnh ($ctype)" || fail "next/image trả $code $ctype"
  s3_run "s3 rm s3://culinary-blog/$key" || true
else
  fail "không ghi được ảnh thử vào S3"
fi

step "8. Trang web + sitemap"
code="$("${CURL[@]}" -o /dev/null -w '%{http_code}' "$BASE/")"
[ "$code" = "200" ] && ok "trang chủ 200" || fail "trang chủ trả $code"
# Job sitemap chạy 02:00 UTC (FR-JOB-003) — trước lần chạy đầu S3 trả 404 NoSuchKey (XML).
# Chỉ kiểm tra request tới được S3, không bị Next.js nuốt.
read -r code ctype < <("${CURL[@]}" -o /dev/null -w '%{http_code} %{content_type}\n' "$BASE/sitemap.xml" | tr -d '\r')
case "$code $ctype" in
  "200 application/xml"*|"404 application/xml"*) ok "/sitemap.xml đi tới S3 ($code)";;
  *) fail "/sitemap.xml trả $code $ctype";;
esac

step "9. Log file của API (FR-OBS-002)"
# MSYS_NO_PATHCONV: Git Bash trên Windows tự đổi "/app/..." thành đường dẫn Windows.
logs="$(MSYS_NO_PATHCONV=1 "${COMPOSE[@]}" exec -T api bash -c 'ls /app/logs' 2>/dev/null | tr -d '\r')"
today="culinaryblog-$(date -u +%Y%m%d).json"
echo "$logs" | grep -qx "$today" && ok "đang ghi $today" || fail "không thấy $today trong /app/logs"
# Kiểm thẳng image (container tạm, KHÔNG gắn volume apilogs): volume giữ log các lần chạy trước là bình thường,
# chỉ image chứa log mới là lỗi (log của máy dev lọt vào lúc build).
baked="$(MSYS_NO_PATHCONV=1 docker run --rm --entrypoint sh culinary-blog/api:prod -c 'ls -A /app/logs 2>/dev/null' | tr -d '\r')"
[ -z "$baked" ] && ok "image không mang log của máy dev" || fail "image chứa file log: $(echo "$baked" | tr '\n' ' ')"

echo
if [ "$FAILED" = "0" ]; then
  printf '\033[1;32mStack production OK.\033[0m\n'
else
  printf '\033[1;31mCó bước lỗi — xem phía trên.\033[0m\n'
  exit 1
fi
