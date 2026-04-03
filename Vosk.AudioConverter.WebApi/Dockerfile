FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Vosk.AudioConverter.WebApi/Vosk.AudioConverter.WebApi.csproj", "Vosk.AudioConverter.WebApi/"]
COPY ["Vosk.Common/Vosk.Common.csproj", "Vosk.Common/"]
RUN dotnet restore "Vosk.AudioConverter.WebApi/Vosk.AudioConverter.WebApi.csproj"
COPY . .
WORKDIR "/src/Vosk.AudioConverter.WebApi"
RUN dotnet build "./Vosk.AudioConverter.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Vosk.AudioConverter.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Vosk.AudioConverter.WebApi.dll"]