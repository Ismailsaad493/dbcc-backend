FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Dbcc.Api/Dbcc.Api.csproj", "Dbcc.Api/"]
RUN dotnet restore "Dbcc.Api/Dbcc.Api.csproj"
COPY . .
WORKDIR "/src/Dbcc.Api"
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dbcc.Api.dll"]