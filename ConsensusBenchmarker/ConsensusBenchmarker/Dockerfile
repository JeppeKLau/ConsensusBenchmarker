FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /ConsensusBenchmarker

# Copy everything
COPY . ./
# Restore as disiting layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /ConsensusBenchmarker
COPY --from=build-env /ConsensusBenchmarker/out .
ENTRYPOINT ["dotnet", "ConsensusBenchmarker.dll"]
