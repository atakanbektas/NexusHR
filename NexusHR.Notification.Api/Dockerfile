FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore \
    "NexusHR.Notification.Api/NexusHR.Notification.Api.csproj"

RUN dotnet publish \
    "NexusHR.Notification.Api/NexusHR.Notification.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "NexusHR.Notification.Api.dll"]