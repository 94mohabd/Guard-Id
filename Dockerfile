# Use the official DlibDotNet runtime image as the base
FROM takuya/dlib-dotnet:runtime-ubuntu-16.04 AS base
WORKDIR /app

# Install additional dependencies (if needed)
RUN apt-get update && apt-get install -y \
    libgdiplus \               # Required for System.Drawing (if used)
    && rm -rf /var/lib/apt/lists/*

# Use the .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project files and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the application code
COPY . ./

# Publish the application to a directory in the container
RUN dotnet publish -c Release -o /out

# Use the DlibDotNet runtime image for the final stage
FROM base AS final
WORKDIR /app

# Copy the published application from the build stage
COPY --from=build /out ./

# Ensure necessary resources are included in the runtime image
COPY Resources/ ./Resources/

# Expose the port the app will run on
EXPOSE 8080

# Define the entry point for the application
ENTRYPOINT ["dotnet", "GuardID.dll"]