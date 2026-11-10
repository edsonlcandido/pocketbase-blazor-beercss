# syntax=docker/dockerfile:1

# ===========================================================================
# Estágio 1: base só com o binário do PocketBase
#
# Serve como alvo isolado (`--target pocketbase`) para rodar o backend
# local sem puxar o SDK do .NET. O compose usa esse alvo.
# ===========================================================================
FROM alpine:latest AS pocketbase

ARG POCKETBASE_VERSION=0.40.5

RUN apk add --no-cache \
    ca-certificates \
    wget \
    unzip

WORKDIR /app

RUN wget https://github.com/pocketbase/pocketbase/releases/download/v${POCKETBASE_VERSION}/pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && unzip pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && rm pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && chmod +x pocketbase

# Estrutura de dados e estáticos (o PB usa --dir/--publicDir/--hooksDir/
# --migrationsDir como irmãos do binário)
RUN mkdir -p pb_hooks pb_migrations pb_data pb_public

EXPOSE 8090

CMD ["./pocketbase", "serve", "--http=0.0.0.0:8090"]

# ===========================================================================
# Estágio 2: build da landing page (Blazor WebAssembly)
# ===========================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS landing-builder

WORKDIR /src

COPY apps/landing ./apps/landing

RUN dotnet publish apps/landing -c Release -o /out/landing

# ===========================================================================
# Estágio 3: build do web app (Blazor WebAssembly)
# ===========================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS web-builder

WORKDIR /src

COPY apps/web ./apps/web

RUN dotnet publish apps/web -c Release -o /out/web

# ===========================================================================
# Estágio 4: imagem final — PocketBase servindo os dois builds
# ===========================================================================
FROM pocketbase AS production

# Hooks e migrations (reconstroem o schema em qualquer ambiente)
COPY pocketbase/pb_hooks ./pb_hooks
COPY pocketbase/pb_migrations ./pb_migrations

# Builds das aplicações
COPY --from=landing-builder /out/landing/wwwroot ./pb_public
COPY --from=web-builder /out/web/wwwroot ./pb_public/app

VOLUME /app/pb_data

CMD ["./pocketbase", "serve", "--http=0.0.0.0:8090"]
