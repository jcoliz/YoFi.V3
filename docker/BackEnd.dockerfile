FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.2 AS build
WORKDIR /source

COPY src/Entities/YoFi.V3.Entities.csproj src/Entities/
COPY src/Directory.Build.props src
COPY scripts/Get-Version.ps1 scripts/
COPY src/ServiceDefaults/YoFi.V3.ServiceDefaults.csproj src/ServiceDefaults/
COPY src/Controllers/YoFi.V3.Controllers.csproj src/Controllers/
COPY src/Data/Sqlite/YoFi.V3.Data.Sqlite.csproj src/Data/Sqlite/
COPY src/Application/YoFi.V3.Application.csproj src/Application/
COPY src/BackEnd/YoFi.V3.BackEnd.csproj src/BackEnd/
WORKDIR /source/src/BackEnd
RUN dotnet restore

# Software version number
#   - Should correspond to tag
#   - Including default value so if someone just runs "docker build", it will work
ARG SOLUTION_VERSION=docker

# copy everything else and build app
WORKDIR /source/src
COPY src/ .
WORKDIR /source/src/BackEnd
RUN dotnet publish --self-contained false -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-rc.2

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Standard health check endpoint
HEALTHCHECK --interval=5s --timeout=5s --start-period=5s --retries=10 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "YoFi.V3.BackEnd.dll"]

# We are listening on 8080, fyi
EXPOSE 8080
