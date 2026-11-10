#!/usr/bin/env python3
"""NFR-MAINT-002 — kiểm line coverage tầng Application từ file cobertura của coverlet.

Dùng:
    python3 scripts/check-coverage.py ./coverage            # ngưỡng mặc định 80%
    python3 scripts/check-coverage.py ./coverage --min 85

Tìm mọi coverage.cobertura.xml trong thư mục kết quả (dotnet test --collect:"XPlat Code Coverage"
--results-directory <dir>), chỉ tính package CulinaryBlog.Application.

- Đếm theo (file, số dòng), lấy số lần chạy lớn nhất: coverlet tách một file thành nhiều class
  (async state machine, lambda) nên cùng một dòng có thể xuất hiện nhiều lần.
- Bỏ code sinh tự động trong obj/ (vd. [GeneratedRegex]) — không phải code nhóm viết.

Thoát 1 khi dưới ngưỡng hoặc không tìm thấy dữ liệu, để CI fail.
"""

import argparse
import collections
import pathlib
import sys
import xml.etree.ElementTree as ET

PACKAGE = "CulinaryBlog.Application"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("results_dir", type=pathlib.Path)
    parser.add_argument("--min", type=float, default=80.0, help="ngưỡng %% line coverage (mặc định 80)")
    parser.add_argument("--top", type=int, default=10, help="in N file thấp nhất")
    args = parser.parse_args()

    reports = sorted(args.results_dir.rglob("coverage.cobertura.xml"))
    if not reports:
        print(f"::error::Không thấy coverage.cobertura.xml trong {args.results_dir}")
        return 1

    hits: dict[str, dict[int, int]] = collections.defaultdict(dict)
    for report in reports:
        for package in ET.parse(report).getroot().iter("package"):
            if package.get("name") != PACKAGE:
                continue
            for cls in package.iter("class"):
                filename = cls.get("filename", "").replace("\\", "/")
                if "/obj/" in filename:
                    continue
                relative = filename.split(f"{PACKAGE}/")[-1]
                lines = hits[relative]
                for line in cls.iter("line"):
                    number = int(line.get("number"))
                    lines[number] = max(lines.get(number, 0), int(line.get("hits", "0")))

    total = sum(len(lines) for lines in hits.values())
    if total == 0:
        print(f"::error::Không có dòng nào của {PACKAGE} trong báo cáo coverage")
        return 1

    covered = sum(1 for lines in hits.values() for h in lines.values() if h > 0)
    percent = covered / total * 100

    print(f"{PACKAGE}: {covered}/{total} dòng = {percent:.1f}% (ngưỡng {args.min:.0f}%, {len(reports)} báo cáo)")

    def file_rate(item):
        _, lines = item
        return sum(1 for h in lines.values() if h > 0) / len(lines)

    print(f"\n{args.top} file thấp nhất:")
    for name, lines in sorted(hits.items(), key=file_rate)[: args.top]:
        file_covered = sum(1 for h in lines.values() if h > 0)
        print(f"  {file_covered / len(lines) * 100:5.1f}%  {file_covered:4d}/{len(lines):<4d} {name}")

    if percent < args.min:
        print(f"\n::error::NFR-MAINT-002: coverage tầng Application {percent:.1f}% < {args.min:.0f}%")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
