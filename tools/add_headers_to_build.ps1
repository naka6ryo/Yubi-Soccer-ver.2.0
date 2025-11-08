<#
Add-NetlifyHeaders.ps1
Usage: run this from repository root after building the Unity WebGL project.

What it does:
- Ensures the target Build folder exists
- Copies the template `_headers` file into the Build folder (renames from template)
- If a `_headers` already exists, it will prompt to overwrite (or use -Force)

Examples:
  # copy and prompt if exists
  .\tools\add_headers_to_build.ps1 -BuildPath .\Build

  # force overwrite
  .\tools\add_headers_to_build.ps1 -BuildPath .\Build -Force

#>
param(
    [string]$BuildPath = ".\Build",
    [switch]$Force
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$template = Join-Path $scriptDir "_headers_build_template.txt"
if (-not (Test-Path $template)) {
    Write-Error "Template file not found: $template"
    exit 2
}

if (-not (Test-Path $BuildPath)) {
    Write-Error "Build path does not exist: $BuildPath"
    exit 3
}

$destHeaders = Join-Path $BuildPath "_headers"
if (Test-Path $destHeaders -and -not $Force) {
    Write-Host "A _headers file already exists at $destHeaders. Use -Force to overwrite." -ForegroundColor Yellow
    exit 0
}

Copy-Item -Path $template -Destination $destHeaders -Force:$Force
if ($?) {
    Write-Host "_headers installed to $destHeaders" -ForegroundColor Green
    Write-Host "Now drag & drop the $BuildPath folder into Netlify (Deploys -> Drag and drop)" -ForegroundColor Cyan
    exit 0
} else {
    Write-Error "Failed to copy _headers to $destHeaders"
    exit 1
}
