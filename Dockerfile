# Build stage: the SDK image restores and publishes; none of it reaches the runtime image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so a change to source code does not invalidate the restore layer
COPY StudyHub.slnx ./
COPY StudyHub.Domain/*.csproj StudyHub.Domain/
COPY StudyHub.Application/*.csproj StudyHub.Application/
COPY StudyHub.Infrastructure/*.csproj StudyHub.Infrastructure/
COPY StudyHub.API/*.csproj StudyHub.API/
RUN dotnet restore StudyHub.API/StudyHub.API.csproj

COPY StudyHub.Domain/ StudyHub.Domain/
COPY StudyHub.Application/ StudyHub.Application/
COPY StudyHub.Infrastructure/ StudyHub.Infrastructure/
COPY StudyHub.API/ StudyHub.API/
RUN dotnet publish StudyHub.API/StudyHub.API.csproj -c Release -o /app/publish --no-restore

# Runtime stage: the Debian-based ASP.NET image on purpose. Alpine and the chiselled
# images ship no time-zone database, and startup refuses an unknown BusinessTime:TimeZoneId
# (ADR-40, ADR-47), so the API would not start at all
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Non-root: the image defines APP_UID for exactly this
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "StudyHub.API.dll"]
