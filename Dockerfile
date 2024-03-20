FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
RUN apt update && apt install curl -y
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CheckinApi.csproj", "./"]
RUN dotnet restore "CheckinApi.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "CheckinApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "CheckinApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ASP.NET Core 8
#ENV ASPNETCORE_HTTP_PORT=5000

#ENV ASPNETCORE_URLS=http://+:5000

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CheckinApi.dll"]
