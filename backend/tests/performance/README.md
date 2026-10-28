# Hướng Dẫn Kiểm Thử Hiệu Năng (Performance Testing)

Bộ script kiểm thử hiệu năng cho hệ thống Culinary Blog theo chuẩn **NFR-PERF-001 .. NFR-PERF-004**.

## 1. Cài Đặt k6
- **Windows (PowerShell)**:
  ```powershell
  winget install k6 --source winget
  # hoặc dùng choco: choco install k6