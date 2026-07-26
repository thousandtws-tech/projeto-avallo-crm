FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props MudBlazorWebApp1.slnx ./
COPY BraSeller.Connectors.Abstractions/BraSeller.Connectors.Abstractions.csproj BraSeller.Connectors.Abstractions/
COPY BraSeller.Connector.MercadoLivre/BraSeller.Connector.MercadoLivre.csproj BraSeller.Connector.MercadoLivre/
COPY MudBlazorWebApp1.Client/MudBlazorWebApp1.Client.csproj MudBlazorWebApp1.Client/
COPY MudBlazorWebApp1/MudBlazorWebApp1.csproj MudBlazorWebApp1/
RUN dotnet restore MudBlazorWebApp1/MudBlazorWebApp1.csproj

COPY BraSeller.Connectors.Abstractions/ BraSeller.Connectors.Abstractions/
COPY BraSeller.Connector.MercadoLivre/ BraSeller.Connector.MercadoLivre/
COPY MudBlazorWebApp1.Client/ MudBlazorWebApp1.Client/
COPY MudBlazorWebApp1/ MudBlazorWebApp1/
RUN dotnet publish MudBlazorWebApp1/MudBlazorWebApp1.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet MudBlazorWebApp1.dll --urls http://0.0.0.0:${PORT:-8080}"]
