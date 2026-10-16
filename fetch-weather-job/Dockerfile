FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY publish/fetch-weather-job/ .

ENTRYPOINT ["dotnet", "fetch-weather-job.dll"]