
# Stage 1: Build and publish the RondiTrack API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files first to cache dependency restoration.
COPY RondiTrack.csproj ./
COPY RondiTrack.Domain/RondiTrack.Domain.csproj RondiTrack.Domain/
COPY RondiTrack.Infrastructure/RondiTrack.Infrastructure.csproj RondiTrack.Infrastructure/

RUN dotnet restore RondiTrack.csproj

# Copy application source files.
COPY Program.cs ./
COPY Authorization/ Authorization/
COPY Common/ Common/
COPY Controllers/ Controllers/
COPY Data/ Data/
COPY Exceptions/ Exceptions/
COPY Health/ Health/
COPY Helpers/ Helpers/
COPY Mapping/ Mapping/
COPY Models/ Models/
COPY Properties/ Properties/
COPY Services/ Services/
COPY Telemetry/ Telemetry/
COPY Validation/ Validation/
COPY RondiTrack.Domain/ RondiTrack.Domain/
COPY RondiTrack.Infrastructure/ RondiTrack.Infrastructure/
COPY appsettings.json ./
COPY appsettings.Development.json ./

# Publish the API.
RUN dotnet publish RondiTrack.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Install EF Core command-line tools.
RUN dotnet tool install --global dotnet-ef --version 10.0.12

ENV PATH="${PATH}:/root/.dotnet/tools"

# Restore dependencies for the Linux migration executable.
RUN dotnet restore RondiTrack.csproj --runtime linux-x64

RUN dotnet restore RondiTrack.Infrastructure/RondiTrack.Infrastructure.csproj \
    --runtime linux-x64

# Create the EF Core migration bundle using dummy build-time settings.
RUN Jwt__Issuer=RondiTrackBuild \
    Jwt__Audience=RondiTrackBuild \
        Jwt__SigningKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA= \
    ConnectionStrings__RondiTrack="Host=localhost;Port=5432;Database=ronditrack;Username=ronditrack;Password=build-only" \
    dotnet ef migrations bundle \
    --project RondiTrack.Infrastructure/RondiTrack.Infrastructure.csproj \
    --startup-project RondiTrack.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained \
    --output /app/efbundle

# Stage 2: Run database migrations before starting the API.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrations

WORKDIR /app

COPY --from=build /app/efbundle /app/efbundle
COPY --from=build /src/appsettings.json /app/appsettings.json

USER $APP_UID

ENTRYPOINT ["/app/efbundle"]

# Stage 3: Run the published API.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "RondiTrack.dll"]
