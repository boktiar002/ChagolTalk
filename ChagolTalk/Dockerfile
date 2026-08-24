# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["ChagolTalk.csproj", "./"]
RUN dotnet restore "ChagolTalk.csproj"

# Copy everything else
COPY . .

# Build and publish
RUN dotnet publish "ChagolTalk.csproj" -c Release -o /app/publish /p:UseAppHost=false


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# Fallback for local `docker run` without -e PORT=...
ENV PORT=8080

# IMPORTANT: this must stay in *shell* form (no brackets), not exec form.
# Exec-form ENTRYPOINT/CMD arrays are passed straight to the OS without a
# shell, so "$PORT" would never be substituted -- Docker would try to start
# dotnet with the literal string "$PORT" as an argument. Render injects PORT
# at container start (not build time), so it can only be read here, when the
# container actually runs.
ENTRYPOINT dotnet ChagolTalk.dll --urls http://0.0.0.0:$PORT
