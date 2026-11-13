FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AIpoweredVivaExamSystem.Presentation/AIpoweredVivaExamSystem.Presentation.csproj", "AIpoweredVivaExamSystem.Presentation/"]
COPY ["AIpoweredVivaExamSystem.Business/AIpoweredVivaExamSystem.Business.csproj", "AIpoweredVivaExamSystem.Business/"]
COPY ["AIpoweredVivaExamSystem.DataAccess/AIpoweredVivaExamSystem.DataAccess.csproj", "AIpoweredVivaExamSystem.DataAccess/"]

RUN dotnet restore "./AIpoweredVivaExamSystem.Presentation/AIpoweredVivaExamSystem.Presentation.csproj"
COPY . .
WORKDIR "/src/AIpoweredVivaExamSystem.Presentation"
RUN dotnet build "./AIpoweredVivaExamSystem.Presentation.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./AIpoweredVivaExamSystem.Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AIpoweredVivaExamSystem.Presentation.dll"]
