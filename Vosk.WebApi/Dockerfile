FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Vosk.WebApi/Vosk.WebApi.csproj", "Vosk.WebApi/"]
RUN dotnet restore "Vosk.WebApi/Vosk.WebApi.csproj"
COPY . .
WORKDIR "/src/Vosk.WebApi"
RUN dotnet build "./Vosk.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Vosk.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish -r linux-amd64 /p:PublishAot=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Vosk.WebApi"]