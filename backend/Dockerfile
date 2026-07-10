# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Real-time Event Analytics Engine.csproj", "./"]

RUN dotnet restore "Real-time Event Analytics Engine.csproj"

COPY . .
RUN dotnet publish "Real-time Event Analytics Engine.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5261
EXPOSE 5261

ENTRYPOINT ["dotnet", "Real-time Event Analytics Engine.dll"]