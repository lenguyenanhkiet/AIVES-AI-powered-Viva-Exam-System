FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AIpoweredVivaExamSystem.Web/AIpoweredVivaExamSystem.Web.csproj", "AIpoweredVivaExamSystem.Web/"]
COPY ["AIpoweredVivaExamSystem.Application/AIpoweredVivaExamSystem.Application.csproj", "AIpoweredVivaExamSystem.Application/"]
COPY ["AIpoweredVivaExamSystem.Domain/AIpoweredVivaExamSystem.Domain.csproj", "AIpoweredVivaExamSystem.Domain/"]
COPY ["AIpoweredVivaExamSystem.Persistence/AIpoweredVivaExamSystem.Persistence.csproj", "AIpoweredVivaExamSystem.Persistence/"]

RUN dotnet restore "./AIpoweredVivaExamSystem.Web/AIpoweredVivaExamSystem.Web.csproj"
COPY . .
WORKDIR "/src/AIpoweredVivaExamSystem.Web"
RUN dotnet build "./AIpoweredVivaExamSystem.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./AIpoweredVivaExamSystem.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AIpoweredVivaExamSystem.Web.dll"]
