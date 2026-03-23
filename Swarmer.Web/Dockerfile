FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Swarmer.Web/Swarmer.Web.csproj", "Swarmer.Web/"]
COPY ["Swarmer.Domain/Swarmer.Domain.csproj", "Swarmer.Domain/"]
RUN dotnet restore "Swarmer.Web/Swarmer.Web.csproj"
COPY . .
WORKDIR "/src/Swarmer.Web"
RUN dotnet build "Swarmer.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Swarmer.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Swarmer.Web.dll"]
