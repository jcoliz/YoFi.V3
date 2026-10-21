FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /source

# Keep package XML documentation for the inherited auth endpoints in Swagger.
ENV NUGET_XMLDOC_MODE=none

COPY Directory.Packages.props ./
COPY src/Directory.Build.props src/
COPY submodules/DotNetNuxt/Directory.Build.props submodules/DotNetNuxt/
COPY submodules/DotNetNuxt/Directory.Packages.props submodules/DotNetNuxt/

COPY src/Entities/YoFi.V3.Entities.csproj src/Entities/
COPY src/ServiceDefaults/YoFi.V3.ServiceDefaults.csproj src/ServiceDefaults/
COPY src/Controllers/YoFi.V3.Controllers.csproj src/Controllers/
COPY src/Data/Sqlite/YoFi.V3.Data.Sqlite.csproj src/Data/Sqlite/
COPY src/Application/YoFi.V3.Application.csproj src/Application/
COPY src/BackEnd/YoFi.V3.BackEnd.csproj src/BackEnd/
COPY submodules/DotNetNuxt/src/Abstractions/DotNetNuxt.Abstractions.csproj submodules/DotNetNuxt/src/Abstractions/
COPY submodules/DotNetNuxt/src/EntityFrameworkCore/DotNetNuxt.EntityFrameworkCore.csproj submodules/DotNetNuxt/src/EntityFrameworkCore/
COPY submodules/DotNetNuxt/src/AspNetCore/DotNetNuxt.AspNetCore.csproj submodules/DotNetNuxt/src/AspNetCore/
COPY submodules/DotNetNuxt/src/Hosting/DotNetNuxt.Hosting.csproj submodules/DotNetNuxt/src/Hosting/

WORKDIR /source/src/BackEnd
RUN dotnet restore

# Software version number
#   - Should correspond to tag
#   - Including default value so if someone just runs "docker build", it will work
ARG SOLUTION_VERSION
ENV SOLUTION_VERSION=${SOLUTION_VERSION:-container}

# copy everything else and build app
WORKDIR /source
COPY src/ src/
COPY submodules/DotNetNuxt/src/ submodules/DotNetNuxt/src/
WORKDIR /source/src/BackEnd
RUN dotnet publish --self-contained false -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Standard health check endpoint
HEALTHCHECK --interval=10s --timeout=5s --retries=5 CMD [ "curl", "--fail", "--silent", "--show-error", "http://localhost:8080/health" ]

ENTRYPOINT ["dotnet", "YoFi.V3.BackEnd.dll"]

# We are listening on 8080, fyi
EXPOSE 8080
