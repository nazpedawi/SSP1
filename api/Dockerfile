FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY publish/api/ .

EXPOSE 8080
ENTRYPOINT ["dotnet", "api.dll"]