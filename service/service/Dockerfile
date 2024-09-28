FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS sdk

WORKDIR /src
COPY . service/
WORKDIR /src/service
RUN dotnet restore --source /src/service/.nuget/packages

ARG BUILD_CONFIGURATION=Release
RUN dotnet build --no-restore "service.csproj" -c $BUILD_CONFIGURATION -o /app/build
RUN dotnet publish --no-restore "service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
COPY --from=sdk /app/publish .
#COPY --from=sdk /src /src
EXPOSE 8080
ENTRYPOINT ["dotnet", "service.dll"]