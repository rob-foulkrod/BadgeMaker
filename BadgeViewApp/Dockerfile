FROM mcr.microsoft.com/dotnet/sdk:8.0 as build

# mkdir -p /app && cd /app
WORKDIR /app

COPY . .

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 as runtime

WORKDIR /app

COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "BadgeViewApp.dll"]