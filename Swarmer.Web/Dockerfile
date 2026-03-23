FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Node.js (required for Tailwind CSS build)
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*

COPY ["Swarmer.Web/Swarmer.Web.csproj", "Swarmer.Web/"]
COPY ["Swarmer.Domain/Swarmer.Domain.csproj", "Swarmer.Domain/"]
RUN dotnet restore "Swarmer.Web/Swarmer.Web.csproj"
COPY . .
WORKDIR "/src/Swarmer.Web"
RUN npm install
RUN dotnet build "Swarmer.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Swarmer.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Swarmer.Web.dll"]
