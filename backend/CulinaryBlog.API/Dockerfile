# CONS-009 — multi-stage: SDK build → aspnet runtime
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# D46 — SkiaSharp có native riêng từng nền tảng: restore/publish theo kiến trúc đích để image chỉ
# chứa libSkiaSharp.so của linux-{x64|arm64} (không RID thì chép native mọi OS, ~440 MB).
# BuildKit tự điền TARGETARCH; giá trị mặc định cho builder cũ. SDK hiểu cả "amd64".
ARG TARGETARCH=amd64

# Copy file build trước để tận dụng layer cache của Docker
COPY Directory.Build.props Directory.Packages.props CulinaryBlog.sln ./
COPY backend/CulinaryBlog.Domain/*.csproj            backend/CulinaryBlog.Domain/
COPY backend/CulinaryBlog.Application/*.csproj       backend/CulinaryBlog.Application/
COPY backend/CulinaryBlog.Infrastructure/*.csproj    backend/CulinaryBlog.Infrastructure/
COPY backend/CulinaryBlog.API/*.csproj               backend/CulinaryBlog.API/
RUN dotnet restore backend/CulinaryBlog.API/CulinaryBlog.API.csproj -a $TARGETARCH

COPY backend/ backend/
RUN dotnet publish backend/CulinaryBlog.API/CulinaryBlog.API.csproj \
    -c Release -a $TARGETARCH --self-contained false -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Image aspnet đã có sẵn ASPNETCORE_HTTP_PORTS=8080 — không đặt thêm ASPNETCORE_URLS (chỉ sinh cảnh báo ghi đè).
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

# Serilog File sink ghi vào /app/logs (FR-OBS-002) — /app thuộc root nên tạo sẵn và giao cho user app, nếu
# không sink lỗi im lặng. Đặt SAU COPY. Volume apilogs (docker-compose.prod.yml) mới tạo nhận quyền từ đây.
RUN mkdir -p /app/logs && chown $APP_UID /app/logs

# Chạy bằng user không phải root. Image aspnet:10.0 (Ubuntu 24.04) không có `adduser` nhưng đã có
# sẵn user `app` (UID trong biến APP_UID = 1654) dành cho việc này.
USER $APP_UID

ENTRYPOINT ["dotnet", "CulinaryBlog.API.dll"]
