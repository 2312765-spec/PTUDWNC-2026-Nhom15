#!/usr/bin/env bash
# =========================================================================================
# KIỂM TRA HIỆU QUẢ REDIS CACHE (NFR-PERF-003)
# Tỷ lệ Cache Hit Rate yêu cầu: >= 80% trong điều kiện steady-state
# =========================================================================================

set -euo pipefail

CONTAINER_NAME="cb-redis"
THRESHOLD=80

echo "=== ĐO TỶ LỆ CACHE HIT CỦA REDIS (CONTAINER: ${CONTAINER_NAME}) ==="

# Kiểm tra container Redis có đang chạy không
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "[-] Lỗi: Container ${CONTAINER_NAME} không hoạt động. Hãy chạy docker compose up -d."
    exit 1
fi

# Lấy thông số stats từ Redis CLI
STATS=$(docker exec -i "${CONTAINER_NAME}" redis-cli INFO stats)

HITS=$(echo "${STATS}" | grep "keyspace_hits:" | cut -d':' -f2 | tr -d '\r')
MISSES=$(echo "${STATS}" | grep "keyspace_misses:" | cut -d':' -f2 | tr -d '\r')

HITS=${HITS:-0}
MISSES=${MISSES:-0}
TOTAL=$((HITS + MISSES))

echo "• Keyspace Hits   : ${HITS}"
echo "• Keyspace Misses : ${MISSES}"
echo "• Tổng yêu cầu    : ${TOTAL}"

if [ "${TOTAL}" -eq 0 ]; then
    echo "[-] Cảnh báo: Chưa có request nào tới Redis (Total = 0). Hãy chạy k6 load test trước."
    exit 1
fi

# Tính toán tỷ lệ phần trăm hit
HIT_RATIO=$(awk "BEGIN {printf \"%.2f\", (${HITS} / ${TOTAL}) * 100}")
echo "• Tỷ lệ Cache Hit : ${HIT_RATIO}%"

# Kiểm tra ngưỡng >= 80% (NFR-PERF-003)
IS_PASS=$(awk "BEGIN {print (${HIT_RATIO} >= ${THRESHOLD}) ? 1 : 0}")

if [ "${IS_PASS}" -eq 1 ]; then
    echo "[+] [PASS] Tỷ lệ Cache Hit đạt ${HIT_RATIO}% (>= ${THRESHOLD}%) theo NFR-PERF-003."
    exit 0
else
    echo "[-] [FAIL] Tỷ lệ Cache Hit ${HIT_RATIO}% chưa đạt chuẩn (yêu cầu >= ${THRESHOLD}%)."
    exit 1
fi