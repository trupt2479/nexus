FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Nexus.NodeService/Nexus.NodeService.csproj", "Nexus.NodeService/"]
COPY ["Nexus.Contracts/Nexus.Contracts.csproj", "Nexus.Contracts/"]
RUN dotnet restore "Nexus.NodeService/Nexus.NodeService.csproj"
COPY . .
WORKDIR "/src/Nexus.NodeService"
RUN dotnet build "Nexus.NodeService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nexus.NodeService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nexus.NodeService.dll"]
