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

# Copy the published application from the build stage
COPY --from=build /out ./ 

# Ensure necessary resources are included in the runtime image
COPY Resources/ ./Resources/

# Optionally, set environment variables for native libraries (if needed)
# ENV DLIB_PATH=/app/Resources

# Expose the port the app will run on
EXPOSE 8080

# Define the entry point for the application
ENTRYPOINT ["dotnet", "GuardID.dll"]
