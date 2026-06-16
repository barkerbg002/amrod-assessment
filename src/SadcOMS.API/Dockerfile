FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/SadcOMS.Domain/SadcOMS.Domain.csproj", "SadcOMS.Domain/"]
COPY ["src/SadcOMS.Infrastructure/SadcOMS.Infrastructure.csproj", "SadcOMS.Infrastructure/"]
COPY ["src/SadcOMS.API/SadcOMS.API.csproj", "SadcOMS.API/"]
RUN dotnet restore "SadcOMS.API/SadcOMS.API.csproj"

COPY src/ .
WORKDIR /src/SadcOMS.API
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SadcOMS.API.dll"]
