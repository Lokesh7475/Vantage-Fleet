# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["*.csproj", "./"]
RUN dotnet restore

# Copy the rest of the code and compile
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Run Stage
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish .

# CHANGE THIS LINE for each service! 
# (e.g., "IngestionService.dll", "StateService.dll", "HistoricalService.dll")
ENTRYPOINT ["dotnet", "IngestionService.dll"]