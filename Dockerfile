# Stage 1: Build the application using the .NET 10 SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files first to cache NuGet package restoration.
COPY RondiTrack.csproj ./
COPY RondiTrack.Domain/RondiTrack.Domain.csproj RondiTrack.Domain/
COPY RondiTrack.Infrastructure/RondiTrack.Infrastructure.csproj RondiTrack.Infrastructure/

RUN dotnet restore RondiTrack.csproj

# Copy application source and referenced projects.
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

# Publish the compiled application.
RUN dotnet publish RondiTrack.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Stage 2: Small runtime-only image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

# Run without root privileges.
USER $APP_UID

# ASP.NET Core listens on port 8080 inside the container.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RondiTrack.dll"]