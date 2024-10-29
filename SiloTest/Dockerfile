# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SiloTest/SiloTest.csproj", "SiloTest/"]
COPY ["src/Orleans.Clustering.Oracle/Orleans.Clustering.Oracle.csproj", "src/Orleans.Clustering.Oracle/"]
COPY ["src/Orleans.Oracle.Core/Orleans.Oracle.Core.csproj", "src/Orleans.Oracle.Core/"]
COPY ["src/Orleans.Persistence.Oracle/Orleans.Persistence.Oracle.csproj", "src/Orleans.Persistence.Oracle/"]
COPY ["src/Orleans.Reminders.Oracle/Orleans.Reminders.Oracle.csproj", "src/Orleans.Reminders.Oracle/"]
COPY ["TestGrain/TestGrain.csproj", "TestGrain/"]
RUN dotnet restore "./SiloTest/SiloTest.csproj"
COPY . .
WORKDIR "/src/SiloTest"
RUN dotnet build "./SiloTest.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SiloTest.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SiloTest.dll"]