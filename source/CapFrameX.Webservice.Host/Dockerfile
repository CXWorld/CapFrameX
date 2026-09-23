#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "CapFrameX.Webservice.Host/CapFrameX.Webservice.Host.csproj"
WORKDIR "/src/CapFrameX.Webservice.Host"
RUN dotnet build "CapFrameX.Webservice.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CapFrameX.Webservice.Host.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CapFrameX.Webservice.Host.dll"]
