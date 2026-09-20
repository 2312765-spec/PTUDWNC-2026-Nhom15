#!/usr/bin/env bash
# Kiểm tra khung dự án còn sạch. Chạy trước khi push.
#   ./scripts/verify.sh
set -uo pipefail

cd "$(dirname "$0")/.."
FAILED=0

step() { printf '\n\033[1;34m==> %s\033[0m\n' "$1"; }
ok()   { printf '\033[0;32m  ✓ %s\033[0m\n' "$1"; }
fail() { printf '\033[0;31m  ✗ %s\033[0m\n' "$1"; FAILED=1; }

step "Backend — .NET"
if ! command -v dotnet >/dev/null 2>&1; then
  fail "Không tìm thấy dotnet. Cài .NET 10 SDK: https://dotnet.microsoft.com/download"
else
  dotnet restore CulinaryBlog.sln >/dev/null 2>&1 \
    && ok "restore" || fail "restore — nếu lỗi 'không tìm thấy version', sửa Directory.Packages.props"

  dotnet build CulinaryBlog.sln --no-restore -c Release >/dev/null 2>&1 \
    && ok "build (0 warning)" || fail "build — chạy 'dotnet build' để xem chi tiết"

  dotnet test backend/tests/CulinaryBlog.ArchitectureTests --no-build -c Release >/dev/null 2>&1 \
    && ok "architecture tests (CONS-001)" || fail "architecture tests — có tầng vi phạm Dependency Rule"

  dotnet test backend/tests/CulinaryBlog.UnitTests --no-build -c Release >/dev/null 2>&1 \
    && ok "unit tests" || fail "unit tests"
fi

step "Frontend — Node"
if ! command -v npm >/dev/null 2>&1; then
  fail "Không tìm thấy npm. Cài Node.js 22 LTS."
else
  pushd frontend >/dev/null
  [ -d node_modules ] || npm ci >/dev/null 2>&1
  npm run lint      >/dev/null 2>&1 && ok "lint"      || fail "lint"
  npm run typecheck >/dev/null 2>&1 && ok "typecheck" || fail "typecheck"
  npm run build     >/dev/null 2>&1 && ok "build"     || fail "build"
  popd >/dev/null
fi

step "Hạ tầng"
if command -v docker >/dev/null 2>&1; then
  docker compose config >/dev/null 2>&1 \
    && ok "docker-compose.yml hợp lệ" || fail "docker-compose.yml có lỗi cú pháp"
else
  printf '  - bỏ qua (không có docker)\n'
fi

step "Secret lọt vào git"
if git ls-files | grep -qE '(^|/)\.env$|appsettings\.Production\.json|\.pfx$|secrets\.json'; then
  fail "CÓ FILE SECRET ĐANG ĐƯỢC TRACK — xem NFR-SEC-007"
else
  ok "không có file secret nào được track"
fi

if [ "$FAILED" -eq 0 ]; then
  printf '\n\033[0;32m✓ Khung sạch. Push được.\033[0m\n'
else
  printf '\n\033[0;31m✗ Có mục thất bại — sửa trước khi push.\033[0m\n'
fi
exit "$FAILED"
