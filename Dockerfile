# Use official .NET SDK image for build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the project files and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the application code
COPY . ./

# Publish the application to a directory in the container
RUN dotnet publish -c Release -o /out

# Use a lightweight .NET runtime image for production
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install native dependencies required by DlibDotNet
RUN apt-get update && apt-get install -y \
    libopenblas-dev \
    liblapack-dev \
    libx11-6 \
    libgdiplus \
    libjpeg-dev \
    libtiff-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy the published application from the build stage
COPY --from=build /out ./

# Ensure necessary resources are included in the runtime image
COPY Resources/ ./Resources/

# Expose the port the app will run on
EXPOSE 8080

# Define the entry point for the application
ENTRYPOINT ["dotnet", "GuardID.dll"]