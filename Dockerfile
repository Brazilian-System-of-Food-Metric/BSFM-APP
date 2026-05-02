# Dockerfile para deploy no Render
# Usa a imagem oficial do .NET SDK para build e runtime

# Estágio de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copia o arquivo de projeto e restaura as dependências
COPY MeusApp.csproj ./
RUN dotnet restore

# Copia todo o código e faz o build
COPY . ./
RUN dotnet publish -c Release -o out

# Estágio de runtime (imagem menor)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Instala dependências necessárias para o SkiaSharp (YoloDotNet)
RUN apt-get update && \
    apt-get install -y libc6-dev libgdiplus libx11-dev && \
    rm -rf /var/lib/apt/lists/*

# Copia o build do estágio anterior
COPY --from=build /app/out .

# Expõe a porta que o Render vai usar
EXPOSE 10000

# Comando de inicialização
ENTRYPOINT ["./MeusApp"]
