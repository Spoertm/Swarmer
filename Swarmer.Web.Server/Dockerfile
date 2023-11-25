FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Swarmer.Web.Server/Swarmer.Web.Server.csproj", "Swarmer.Web.Server/"]
COPY ["Swarmer.Domain/Swarmer.Domain.csproj", "Swarmer.Domain/"]
COPY ["Swarmer.Web.Client/Swarmer.Web.Client.csproj", "Swarmer.Web.Client/"]
RUN dotnet restore "Swarmer.Web.Server/Swarmer.Web.Server.csproj"
COPY . .
WORKDIR "/src/Swarmer.Web.Server"
RUN dotnet build "Swarmer.Web.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Swarmer.Web.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Swarmer.Web.Server.dll"]
