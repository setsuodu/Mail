FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
RUN apt-get update && apt-get install -y clang zlib1g-dev && rm -rf /var/lib/apt/lists/*

COPY server/src/Mail.Server.Api/Mail.Server.Api.csproj Mail.Server.Api/
RUN dotnet restore Mail.Server.Api/Mail.Server.Api.csproj

COPY server/src/Mail.Server.Api/ Mail.Server.Api/
WORKDIR /src/Mail.Server.Api
RUN dotnet publish Mail.Server.Api.csproj -c Release -r linux-x64 -o /app/publish -p:PublishAot=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["./Mail.Server.Api"]
