#!/usr/bin/env bash
# =========================================================================================
# KIỂM TRA HIỆU QUẢ REDIS CACHE (NFR-PERF-003)
# Yêu cầu: cache hit rate >= 80% trong điều kiện steady-state.
#
# keyspace_hits/misses của Redis cộng dồn từ lúc khởi động, nên phải xóa bộ đếm SAU khi
# làm nóng cache, rồi mới chạy tải:
#   ./check-redis-cache.sh reset     # sau smoke-test (làm nóng)
#   k6 run load-test.js
#   ./check-redis-cache.sh           # đọc tỷ lệ hit trong lượt load test
#
# REDIS_CONTAINER: tên container Redis (mặc định cb-redis của docker-compose.yml).
# =========================================================================================

set -euo pipefail

CONTAINER_NAME="${REDIS_CONTAINER:-cb-redis}"
THRESHOLD=80

if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "[-] Lỗi: container ${CONTAINER_NAME} không chạy. Hãy chạy docker compose up -d."
    exit 1
fi

if [ "${1:-}" = "reset" ]; then
    docker exec "${CONTAINER_NAME}" redis-cli CONFIG RESETSTAT > /dev/null
    echo "[+] Đã xóa bộ đếm keyspace_hits/misses của ${CONTAINER_NAME}."
    exit 0
fi

echo "=== ĐO TỶ LỆ CACHE HIT CỦA REDIS (CONTAINER: ${CONTAINER_NAME}) ==="

STATS=$(docker exec "${CONTAINER_NAME}" redis-cli INFO stats)

HITS=$(echo "${STATS}" | grep "keyspace_hits:" | cut -d':' -f2 | tr -d '\r')
MISSES=$(echo "${STATS}" | grep "keyspace_misses:" | cut -d':' -f2 | tr -d '\r')

HITS=${HITS:-0}
MISSES=${MISSES:-0}
TOTAL=$((HITS + MISSES))

echo "• Keyspace Hits   : ${HITS}"
echo "• Keyspace Misses : ${MISSES}"
echo "• Tổng lượt đọc   : ${TOTAL}"

if [ "${TOTAL}" -eq 0 ]; then
    echo "[-] Chưa có lượt đọc nào tới Redis kể từ lần reset. Hãy chạy k6 load test trước."
    exit 1
fi

HIT_RATIO=$(awk "BEGIN {printf \"%.2f\", (${HITS} / ${TOTAL}) * 100}")
echo "• Tỷ lệ Cache Hit : ${HIT_RATIO}%"

IS_PASS=$(awk "BEGIN {print (${HIT_RATIO} >= ${THRESHOLD}) ? 1 : 0}")

if [ "${IS_PASS}" -eq 1 ]; then
    echo "[+] [PASS] Tỷ lệ Cache Hit ${HIT_RATIO}% (>= ${THRESHOLD}%) — NFR-PERF-003."
    exit 0
else
    echo "[-] [FAIL] Tỷ lệ Cache Hit ${HIT_RATIO}% chưa đạt (yêu cầu >= ${THRESHOLD}%)."
    exit 1
fi
