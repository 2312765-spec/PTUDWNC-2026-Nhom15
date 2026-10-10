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
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Chạy bằng user không phải root
RUN adduser --disabled-password --gecos "" --uid 1001 appuser
USER appuser

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CulinaryBlog.API.dll"]
