//Đo trực tiếp từ Redis INFO để xác minh Tỷ lệ cache hit ≥ 80% (NFR-PERF-003)

#!/usr/bin/env bash
# Đo tỷ lệ Cache Hit của Redis từ container Docker
set -e

CONTAINER_NAME=${1:-ptudwnc-2026-nhom15-redis-1}

# Lấy số liệu stats từ Redis CLI
STATS=$(docker exec "$CONTAINER_NAME" redis-cli INFO stats 2>/dev/null || redis-cli INFO stats)

HITS=$(echo "$STATS" | grep "keyspace_hits:" | awk -F':' '{print $2}' | tr -d '\r')
MISSES=$(echo "$STATS" | grep "keyspace_misses:" | awk -F':' '{print $2}' | tr -d '\r')

HITS=${HITS:-0}
MISSES=${MISSES:-0}
TOTAL=$((HITS + MISSES))

echo "=================================================="
echo "          REDIS CACHE HIT RATE VERIFIER           "
echo "=================================================="
echo "Keyspace Hits   : $HITS"
echo "Keyspace Misses : $MISSES"
echo "Total Lookups   : $TOTAL"

if [ "$TOTAL" -eq 0 ]; then
  echo "Chưa có lượt truy vấn cache nào. Hãy chạy test k6 trước."
  exit 0
fi

# Tính tỷ lệ phần trăm (dùng awk để có số thập phân)
HIT_RATE=$(awk -v h="$HITS" -v t="$TOTAL" 'BEGIN { printf "%.2f", (h / t) * 100 }')
echo "Cache Hit Ratio : $HIT_RATE %"
echo "Target NFR-PERF-003: >= 80.00 %"

PASSED=$(awk -v r="$HIT_RATE" 'BEGIN { if (r >= 80.0) print "1"; else print "0" }')

if [ "$PASSED" -eq "1" ]; then
  echo "[PASS] Đạt chuẩn NFR-PERF-003 (Tỷ lệ cache hit >= 80%)"
else
  echo "[FAIL] Chưa đạt 80%. Hãy chạy k6 load-test thêm 2-3 phút để cache phủ đầy."
fi
echo "=================================================="