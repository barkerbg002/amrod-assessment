FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/SadcOMS.Domain/SadcOMS.Domain.csproj", "SadcOMS.Domain/"]
COPY ["src/SadcOMS.Infrastructure/SadcOMS.Infrastructure.csproj", "SadcOMS.Infrastructure/"]
COPY ["src/SadcOMS.Worker/SadcOMS.Worker.csproj", "SadcOMS.Worker/"]
RUN dotnet restore "SadcOMS.Worker/SadcOMS.Worker.csproj"

COPY src/ .
WORKDIR /src/SadcOMS.Worker
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SadcOMS.Worker.dll"]
