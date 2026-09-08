# Built by docker compose from the repository root:
#   docker compose build supplier-fake
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the build configuration first so that restore is cached independently of source changes.
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props global.json ./
COPY src/Supplier.Api.Fake/Supplier.Api.Fake.csproj src/Supplier.Api.Fake/
RUN dotnet restore src/Supplier.Api.Fake/Supplier.Api.Fake.csproj

COPY src/Supplier.Api.Fake/ src/Supplier.Api.Fake/
RUN dotnet publish src/Supplier.Api.Fake/Supplier.Api.Fake.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The aspnet image ships without curl, and docker compose needs something to probe /health with.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Supplier.Api.Fake.dll"]
