FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AIpoweredVivaExamSystem.Api/AIpoweredVivaExamSystem.Api.csproj", "AIpoweredVivaExamSystem.Api/"]
COPY ["AIpoweredVivaExamSystem.Application/AIpoweredVivaExamSystem.Application.csproj", "AIpoweredVivaExamSystem.Application/"]
COPY ["AIpoweredVivaExamSystem.Domain/AIpoweredVivaExamSystem.Domain.csproj", "AIpoweredVivaExamSystem.Domain/"]
COPY ["AIpoweredVivaExamSystem.Infrastructure/AIpoweredVivaExamSystem.Infrastructure.csproj", "AIpoweredVivaExamSystem.Infrastructure/"]
COPY ["AIpoweredVivaExamSystem.Persistence/AIpoweredVivaExamSystem.Persistence.csproj", "AIpoweredVivaExamSystem.Persistence/"]

RUN dotnet restore "./AIpoweredVivaExamSystem.Api/AIpoweredVivaExamSystem.Api.csproj"
COPY . .
WORKDIR "/src/AIpoweredVivaExamSystem.Api"
RUN dotnet build "./AIpoweredVivaExamSystem.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./AIpoweredVivaExamSystem.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AIpoweredVivaExamSystem.Api.dll"]