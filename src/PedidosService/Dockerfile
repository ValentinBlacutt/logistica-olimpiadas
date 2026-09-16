FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/PedidosService/PedidosService.csproj", "src/PedidosService/"]
RUN dotnet restore "src/PedidosService/PedidosService.csproj"
COPY . .
WORKDIR "/src/src/PedidosService"
RUN dotnet build "PedidosService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PedidosService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PedidosService.dll"]