# STAGE 1: PREPARE
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Copy csproj 
COPY ["HNProxyAPI/HNProxyAPI.csproj", "HNProxyAPI/"]

# 2. Restore dependencies
RUN dotnet restore "HNProxyAPI/HNProxyAPI.csproj"

# 3. Copy all other files
COPY . .

# 4. Publish (Build is implicit)
WORKDIR "/src/HNProxyAPI"
RUN dotnet publish -c Release -o /app/publish

# STAGE 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# CORREÇÃO CRÍTICA AQUI:
# --from=build: 
# /app/publish: 
COPY --from=build /app/publish .

# Expose port 8080 (default for .NET 8+)
EXPOSE 8080

ENTRYPOINT ["dotnet", "HNProxyAPI.dll"]
