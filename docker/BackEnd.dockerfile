FROM mcr.microsoft.com/dotnet/sdk:10.0.100-rc.2 AS build
WORKDIR /source

COPY src/BackEnd/YoFi.V3.BackEnd.csproj BackEnd/
COPY src/ServiceDefaults/YoFi.V3.ServiceDefaults.csproj ServiceDefaults/
COPY src/Controllers/YoFi.V3.Controllers.csproj Controllers/
COPY src/Application/YoFi.V3.Application.csproj Application/
COPY src/Entities/YoFi.V3.Entities.csproj Entities/
COPY src/Directory.build.props Directory.Build.props

WORKDIR /source/BackEnd
RUN dotnet restore

# Software version number
#   - Should correspond to tag
#   - Including default value so if someone just runs "docker build", it will work
ARG SOLUTION_VERSION=docker

# copy everything else and build app
WORKDIR /source
COPY src/ .
WORKDIR /source/BackEnd
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
