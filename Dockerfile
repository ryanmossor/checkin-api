FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt update && apt install curl -y
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /app
COPY ["src/CheckinApi.csproj", "./"]
RUN dotnet restore "CheckinApi.csproj"
COPY . .
WORKDIR "/app/src/"
RUN dotnet build "CheckinApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "CheckinApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir -p /app/data/requests /app/data/results

ENTRYPOINT ["dotnet", "CheckinApi.dll"]
