FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["TechMove/TechMove.csproj", "TechMove/"]
COPY ["TechMove.Contracts/TechMove.Contracts.csproj", "TechMove.Contracts/"]
RUN dotnet restore "TechMove/TechMove.csproj"
COPY . .
WORKDIR "/src/TechMove"
RUN dotnet publish "TechMove.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TechMove.dll"]
