# Multi-stage build for the Learnexia modular monolith (.NET 10).
# Build context is the REPO ROOT (see docker-compose.yaml `build.context: ..`) so this
# Dockerfile can COPY the whole `backend/` tree, central package versions, and Directory.Build.props.
# Entrypoint is the Host project -> Learnexia.Host.dll.

# ---- Runtime base (ASP.NET 10) ---------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# ---- Build (SDK 10) --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy central build/version files first so restore layers cache well.
COPY ["backend/Directory.Packages.props", "backend/"]
COPY ["backend/Directory.Build.props", "backend/"]
COPY ["backend/Learnexia.Modular.sln", "backend/"]

# Copy the full backend source tree, then restore + publish the solution.
COPY ["backend/", "backend/"]

WORKDIR /src/backend
RUN dotnet restore "Learnexia.Modular.sln"
RUN dotnet publish "src/Host/Learnexia.Host/Learnexia.Host.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Final runtime image ---------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Learnexia.Host.dll"]
