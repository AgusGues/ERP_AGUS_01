# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj & restore dependencies
COPY ERP_AGUS_01/*.csproj ./ERP_AGUS_01/
RUN dotnet restore ERP_AGUS_01/*.csproj

# Copy seluruh kode & publish
COPY ERP_AGUS_01/. ./ERP_AGUS_01/
RUN dotnet publish ERP_AGUS_01/ -c Release -o /app

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 80

# Jalankan aplikasi
ENTRYPOINT ["dotnet", "ERP_AGUS_01.dll"]
