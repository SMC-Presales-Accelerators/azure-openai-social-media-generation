# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-azurelinux3.0-distroless-composite AS base
USER app
WORKDIR /app
EXPOSE 8080


# Install node 
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS with-node
RUN apt-get update
RUN apt-get install curl
RUN curl -sL https://deb.nodesource.com/setup_22.x | bash
RUN apt-get -y install nodejs


FROM with-node AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["azure-openai-social-media-generation.Server/azure-openai-social-media-generation.Server.csproj", "azure-openai-social-media-generation.Server/"]
COPY ["azure-openai-social-media-generation.client/azure-openai-social-media-generation.client.esproj", "azure-openai-social-media-generation.client/"]
RUN dotnet restore "./azure-openai-social-media-generation.Server/azure-openai-social-media-generation.Server.csproj"
COPY . .
WORKDIR "/src/azure-openai-social-media-generation.Server"
RUN dotnet build "./azure-openai-social-media-generation.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./azure-openai-social-media-generation.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "azure-openai-social-media-generation.Server.dll"]
