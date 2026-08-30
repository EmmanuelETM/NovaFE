# Imagen de la API NovaFE. Cuatro etapas (base → build → publish → final).
# Visual Studio usa este archivo tanto para el "Fast Mode" de depuración (monta
# el binario sobre `base`) como para construir la imagen real (`final`).
# El contexto de build es la raíz del repositorio, no esta carpeta.
# Ver https://aka.ms/customizecontainer

# ==========================================
#      Etapa base: runtime mínimo
# ==========================================
# 'aspnet' (no 'sdk'): imagen chica, sin compilador ni SDK en producción.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
# 8080 es el puerto por defecto de las imágenes .NET desde la versión 8. El 8081
# solo lo usa el perfil "Container (Dockerfile)" de Visual Studio (HTTPS con el
# certificado de desarrollo que VS monta); el compose publica únicamente el 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
EXPOSE 8081

# ==========================================
#           Etapa build: compilación
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Los .csproj se copian ANTES que el código: mientras no cambien ellos ni las
# versiones de paquetes, Docker reutiliza la capa de restore aunque edites un .cs.
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY [".editorconfig", "."]
COPY ["src/Service/NovaFE.Service.csproj", "src/Service/"]
COPY ["src/Application/NovaFE.Application.csproj", "src/Application/"]
COPY ["src/Domain/NovaFE.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/NovaFE.Infrastructure.csproj", "src/Infrastructure/"]

# Solo el proyecto de servicio: arrastra las otras tres capas por referencia y
# deja fuera los paquetes de pruebas (Testcontainers, xUnit).
RUN dotnet restore "./src/Service/NovaFE.Service.csproj"

COPY src/ src/
WORKDIR "/src/src/Service"
RUN dotnet build "./NovaFE.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# ==========================================
#           Etapa publish: publicación
# ==========================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NovaFE.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

# ==========================================
#             Etapa final: runtime
# ==========================================
FROM base AS final

# curl solo para el HEALTHCHECK: la imagen aspnet no trae cliente HTTP. Se
# instala como root y se vuelve al usuario sin privilegios.
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID

WORKDIR /app
COPY --from=publish /app/publish .

# /health/live no toca la base de datos: responde "el proceso está vivo". Usar
# /health/ready aquí reiniciaría el contenedor cada vez que PostgreSQL tuviera un
# hipo, que es lo contrario de lo que se quiere.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "NovaFE.Service.dll"]
