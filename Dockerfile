# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CoffeeShop.csproj", "./"]
RUN dotnet restore "CoffeeShop.csproj"
COPY . .
RUN dotnet publish "CoffeeShop.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Container
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CoffeeShop.dll"]
