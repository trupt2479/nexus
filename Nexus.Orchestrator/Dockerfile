FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Nexus.Orchestrator/Nexus.Orchestrator.csproj", "Nexus.Orchestrator/"]
COPY ["Nexus.Contracts/Nexus.Contracts.csproj", "Nexus.Contracts/"]
RUN dotnet restore "Nexus.Orchestrator/Nexus.Orchestrator.csproj"
COPY . .
WORKDIR "/src/Nexus.Orchestrator"
RUN dotnet build "Nexus.Orchestrator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nexus.Orchestrator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nexus.Orchestrator.dll"]
