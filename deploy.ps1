<#
.SYNOPSIS
  Builds and deploys the Weather Images app to Azure.

.DESCRIPTION
  1. Creates the resource group
  2. Bicep phase 1: storage (queues, blob container, table) + container registry
  3. Publishes the 3 projects as container images with the dotnet CLI and pushes them to the registry
  4. Bicep phase 2: Container Apps environment, API and the two queue-triggered jobs
  5. Prints the API URL

.EXAMPLE
  $env:UNSPLASH_ACCESS_KEY = "<your Unsplash access key>"
  .\deploy.ps1

.EXAMPLE
  .\deploy.ps1 -Suffix mh26 -Location westeurope -AppsLocation swedencentral -UnsplashAccessKey "<key>"
#>
param(
    [string]$Suffix = "np",                            # short unique suffix for all resource names
    [string]$Location = "germanywestcentral",          # storage + registry
    [string]$AppsLocation = "swedencentral",           # Container Apps environment, API and jobs
    [string]$UnsplashAccessKey = $env:UNSPLASH_ACCESS_KEY
)

# Stop the script with a clear message when a command fails
function Assert-Success([string]$step) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $step" -ForegroundColor Red
        exit 1
    }
}

function Write-Step([string]$text) {
    Write-Host ""
    Write-Host "=== $text ===" -ForegroundColor Cyan
}

# Always run from the folder this script is in (the repo root)
Set-Location $PSScriptRoot

$ResourceGroup = "ssp1-rg-$Suffix"
$RegistryName  = "ssp1acr$Suffix"
$LoginServer   = "$RegistryName.azurecr.io"
$ImageTag      = Get-Date -Format "yyyyMMddHHmmss"   # a new tag every deploy, so Azure always picks up the new images

if ([string]::IsNullOrWhiteSpace($UnsplashAccessKey)) {
    Write-Host "No Unsplash key. Set `$env:UNSPLASH_ACCESS_KEY or pass -UnsplashAccessKey." -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------------------
Write-Step "Checking Azure login"
az account show --output none
if ($LASTEXITCODE -ne 0) {
    az login --output none
    Assert-Success "az login"
}
az account show --query "{subscription:name, user:user.name}" --output table

# ---------------------------------------------------------------------------
Write-Step "Registering Azure resource providers (only slow the first time)"
foreach ($provider in "Microsoft.Storage", "Microsoft.ContainerRegistry", "Microsoft.ManagedIdentity", "Microsoft.App", "Microsoft.OperationalInsights") {
    az provider register --namespace $provider --wait --output none
    Assert-Success "register $provider"
}

# ---------------------------------------------------------------------------
Write-Step "Creating resource group $ResourceGroup in $Location"
az group create --name $ResourceGroup --location $Location --output none
Assert-Success "az group create"

# ---------------------------------------------------------------------------
Write-Step "Bicep phase 1: storage + container registry"
az deployment group create `
    --resource-group $ResourceGroup `
    --template-file infra/main.bicep `
    --parameters suffix=$Suffix location=$Location appsLocation=$AppsLocation deployApps=false `
    --output none
Assert-Success "Bicep phase 1"

# ---------------------------------------------------------------------------
Write-Step "Logging in to the container registry $RegistryName"
# Docker Desktop running: use the normal login.
# Otherwise: get a token and write it to a temporary Docker config that only this script uses.
az acr login --name $RegistryName --output none 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker is not running, using a token login instead"
    $token = az acr login --name $RegistryName --expose-token --query accessToken --output tsv 2>$null
    Assert-Success "az acr login --expose-token"

    $dockerConfigDir = Join-Path $env:TEMP "ssp1-docker-config"
    New-Item -ItemType Directory -Force $dockerConfigDir | Out-Null
    $auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("00000000-0000-0000-0000-000000000000:$token"))
    @{ auths = @{ $LoginServer = @{ auth = $auth } } } | ConvertTo-Json -Depth 5 |
        Set-Content (Join-Path $dockerConfigDir "config.json")
    $env:DOCKER_CONFIG = $dockerConfigDir
}

# ---------------------------------------------------------------------------
Write-Step "Publishing the 3 projects as container images (tag $ImageTag) with the dotnet CLI"
$projects = @(
    @{ Path = "api";               Repository = "api" },
    @{ Path = "fetch-weather-job"; Repository = "fetch-weather-job" },
    @{ Path = "process-image-job"; Repository = "process-image-job" }
)
foreach ($project in $projects) {
    Write-Host "-> $($project.Path)"
    dotnet publish $project.Path -c Release --os linux --arch x64 /t:PublishContainer `
        -p EnableSdkContainerSupport=true `
        -p ContainerRegistry=$LoginServer `
        -p ContainerRepository=$($project.Repository) `
        -p ContainerImageTag=$ImageTag
    Assert-Success "dotnet publish $($project.Path)"
}

# ---------------------------------------------------------------------------
Write-Step "Bicep phase 2: Container Apps environment, API and jobs"
az deployment group create `
    --resource-group $ResourceGroup `
    --template-file infra/main.bicep `
    --parameters suffix=$Suffix location=$Location appsLocation=$AppsLocation deployApps=true imageTag=$ImageTag unsplashAccessKey=$UnsplashAccessKey `
    --output none
Assert-Success "Bicep phase 2"

# ---------------------------------------------------------------------------
Write-Step "Done"
$fqdn = az resource show --resource-group $ResourceGroup --name "ssp1-api-$Suffix" `
    --resource-type Microsoft.App/containerApps `
    --query properties.configuration.ingress.fqdn --output tsv
Write-Host "API URL: https://$fqdn" -ForegroundColor Green
Write-Host "Use it as @baseUrl in api/api.http"