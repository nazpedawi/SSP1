FROM mcr.microsoft.com/dotnet/runtime:10.0

# Linux containers have no fonts installed. ImageHelper needs a font to draw the
# weather text, so install DejaVu (one of the fallbacks in ImageHelper.ResolveFont)
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY publish/process-image-job/ .

ENTRYPOINT ["dotnet", "process-image-job.dll"]