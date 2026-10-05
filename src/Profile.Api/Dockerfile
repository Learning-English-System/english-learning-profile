FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

COPY Directory.Build.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Auth/Auth.Contracts/ src/Auth/Auth.Contracts/
COPY src/Profile/ src/Profile/

RUN dotnet restore src/Profile/Profile.Api/Profile.Api.csproj
RUN dotnet publish src/Profile/Profile.Api/Profile.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5002
EXPOSE 5002

ENTRYPOINT ["dotnet", "Profile.Api.dll"]
