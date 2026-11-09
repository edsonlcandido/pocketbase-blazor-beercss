# syntax=docker/dockerfile:1

# ===========================================================================
# Estágio 1: build da landing page (Blazor WebAssembly)
# ===========================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS landing-builder

WORKDIR /src

COPY apps/landing ./apps/landing

RUN dotnet publish apps/landing -c Release -o /out/landing

# ===========================================================================
# Estágio 2: build do web app (Blazor WebAssembly)
# ===========================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS web-builder

WORKDIR /src

COPY apps/web ./apps/web

RUN dotnet publish apps/web -c Release -o /out/web

# ===========================================================================
# Estágio 3: imagem final — PocketBase servindo os dois builds
# ===========================================================================
FROM alpine:latest

RUN apk add --no-cache \
    ca-certificates \
    wget \
    unzip

WORKDIR /app

# Baixar e instalar PocketBase
ARG POCKETBASE_VERSION=0.40.5
RUN wget https://github.com/pocketbase/pocketbase/releases/download/v${POCKETBASE_VERSION}/pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && unzip pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && rm pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && chmod +x pocketbase

# Estrutura de dados e estáticos
RUN mkdir -p pb_hooks pb_migrations pb_data pb_public/app

# Hooks e migrations (reconstroem o schema em qualquer ambiente)
COPY pocketbase/pb_hooks ./pb_hooks
COPY pocketbase/pb_migrations ./pb_migrations

# Builds das aplicações
COPY --from=landing-builder /out/landing/wwwroot ./pb_public
COPY --from=web-builder /out/web/wwwroot ./pb_public/app

EXPOSE 8090

VOLUME /app/pb_data

CMD ["./pocketbase", "serve", "--http=0.0.0.0:8090"]
