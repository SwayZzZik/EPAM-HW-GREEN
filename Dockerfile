
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore GreenAndTasty.Backend.csproj
RUN dotnet publish GreenAndTasty.Backend.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV APP_DATA_DIR=/tmp/green-and-tasty-data

EXPOSE 10000

ENTRYPOINT ["dotnet", "GreenAndTasty.Backend.dll"]
