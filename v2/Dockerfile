# ============================================================================
# BSFM v2 — imagem única: React compilado + API .NET.
#
# Multi-stage:
#   1. web     — compila o React (Vite) para estático
#   2. build   — compila e publica a API
#   3. final   — runtime ASP.NET com o front servido em wwwroot
# ============================================================================

# ---------------------------------------------------------------- front ----
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json ./
RUN npm install --no-audit --no-fund
COPY web/ ./
RUN npm run build

# ---------------------------------------------------------------- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria: só refaz quando um .csproj muda.
COPY backend/BSFM.Seguranca/BSFM.Seguranca.csproj            backend/BSFM.Seguranca/
COPY backend/BSFM.Dados/BSFM.Dados.csproj                    backend/BSFM.Dados/
COPY backend/BSFM.Nutricao/BSFM.Nutricao.csproj              backend/BSFM.Nutricao/
COPY backend/BSFM.Api/BSFM.Api.csproj                        backend/BSFM.Api/
RUN dotnet restore backend/BSFM.Api/BSFM.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/BSFM.Api/BSFM.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# -------------------------------------------------------------- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000

COPY --from=build /app/publish ./
COPY --from=web   /web/dist     ./wwwroot

EXPOSE 10000
ENTRYPOINT ["dotnet", "BSFM.Api.dll"]
