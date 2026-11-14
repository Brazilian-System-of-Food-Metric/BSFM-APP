# ============================================================================
# BSFM v2 — imagem única: API .NET 10 + front React compilado.
#
# Este Dockerfile fica na RAIZ do repositório de propósito: o Render procura
# "Dockerfile" na raiz por padrão, então não é preciso configurar
# dockerfilePath/dockerContext no Blueprint — menos um ponto de falha no deploy.
#
# Estágios:
#   1. web    — compila o React (Vite) para estático
#   2. build  — compila e publica a API
#   3. final  — runtime ASP.NET servindo a API e o front em wwwroot
# ============================================================================

# ---------------------------------------------------------------- front ----
FROM node:22-alpine AS web
WORKDIR /web
COPY v2/web/package.json ./
RUN npm install --no-audit --no-fund
COPY v2/web/ ./
RUN npm run build

# ---------------------------------------------------------------- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria: só refaz quando um .csproj muda.
COPY v2/backend/BSFM.Seguranca/BSFM.Seguranca.csproj  v2/backend/BSFM.Seguranca/
COPY v2/backend/BSFM.Dados/BSFM.Dados.csproj          v2/backend/BSFM.Dados/
COPY v2/backend/BSFM.Nutricao/BSFM.Nutricao.csproj    v2/backend/BSFM.Nutricao/
COPY v2/backend/BSFM.Api/BSFM.Api.csproj              v2/backend/BSFM.Api/
RUN dotnet restore v2/backend/BSFM.Api/BSFM.Api.csproj

COPY v2/backend/ v2/backend/
RUN dotnet publish v2/backend/BSFM.Api/BSFM.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# -------------------------------------------------------------- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000

COPY --from=build /app/publish ./
COPY --from=web   /web/dist     ./wwwroot

EXPOSE 10000
ENTRYPOINT ["dotnet", "BSFM.Api.dll"]
