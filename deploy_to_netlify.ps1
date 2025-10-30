<#
Helper script to deploy the TemplateData folder to Netlify using the Netlify CLI.
Requires: Node + npm, `npm i -g netlify-cli`, and `netlify login` beforehand.

Usage:
  .\deploy_to_netlify.ps1 -SiteId <your-netlify-site-id> [-Prod]

If you omit -SiteId the script will run an interactive deploy.
#>
param(
    [string]$SiteId = $null,
    [switch]$Prod = $false
)

$publishDir = Join-Path $PSScriptRoot 'Assets\WebGLTemplates\TemplateData'
if (-not (Test-Path $publishDir)) {
    Write-Error "Publish directory not found: $publishDir"
    exit 1
}

Write-Host "Using publish dir: $publishDir"

if (-not (Get-Command netlify -ErrorAction SilentlyContinue)) {
    Write-Error "Netlify CLI not found. Install with: npm i -g netlify-cli"
    exit 1
}

if ($SiteId) {
    if ($Prod) {
        Write-Host "Deploying to Netlify site $SiteId (production)..."
        netlify deploy --dir=$publishDir --site=$SiteId --prod
    } else {
        Write-Host "Deploying to Netlify site $SiteId (draft)..."
        netlify deploy --dir=$publishDir --site=$SiteId
    }
} else {
    Write-Host "Interactive deploy (you will choose site or create new)"
    if ($Prod) {
        netlify deploy --dir=$publishDir --prod
    } else {
        netlify deploy --dir=$publishDir
    }
}
