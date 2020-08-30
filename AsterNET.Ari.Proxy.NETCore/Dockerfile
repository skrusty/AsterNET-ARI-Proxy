#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:2.1-stretch-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:2.1-stretch AS build
WORKDIR /src
COPY ["AsterNET.Ari.Proxy.NETCore/AsterNET.Ari.Proxy.NETCore.csproj", "AsterNET.Ari.Proxy.NETCore/"]
COPY ["AsterNET.ARI.Proxy.Providers.RabbitMQ.NETStandard/AsterNET.ARI.Proxy.Providers.RabbitMQ.NETStandard.csproj", "AsterNET.ARI.Proxy.Providers.RabbitMQ.NETStandard/"]
COPY ["AsterNET.ARI.Proxy.Common.NetStandard/AsterNET.ARI.Proxy.Common.NetStandard.csproj", "AsterNET.ARI.Proxy.Common.NetStandard/"]
RUN dotnet restore "AsterNET.Ari.Proxy.NETCore/AsterNET.Ari.Proxy.NETCore.csproj"
COPY . .
WORKDIR "/src/AsterNET.Ari.Proxy.NETCore"
RUN dotnet build "AsterNET.Ari.Proxy.NETCore.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AsterNET.Ari.Proxy.NETCore.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "asternet-ari-proxy.dll"]