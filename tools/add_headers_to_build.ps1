<#
add_headers_to_build.ps1
Creates a Netlify `_headers` file in the specified Build folder.
This script writes UTF-8 without BOM to avoid Netlify parsing issues.

Usage:
  # from repository root
  .\tools\add_headers_to_build.ps1 -BuildPath .\Build
  # force overwrite
  .\tools\add_headers_to_build.ps1 -BuildPath .\Build -Force

#>

param(
    [string]$BuildPath = ".\Build",
    [switch]$Force
)

Write-Host "Creating _headers in: $BuildPath"

if (-not (Test-Path $BuildPath)) {
    Write-Host "Build path not found. Creating: $BuildPath"
    New-Item -ItemType Directory -Path $BuildPath -Force | Out-Null
}

$headersContent = @'
/*.js.br
  Content-Type: application/javascript
  Content-Encoding: br
  Cache-Control: public, max-age=31536000, immutable

/*.framework.js.br
  Content-Type: application/javascript
  Content-Encoding: br
  Cache-Control: public, max-age=31536000, immutable

/*.data.br
  Content-Type: application/octet-stream
  Content-Encoding: br
  Cache-Control: public, max-age=31536000, immutable

/*.wasm.br
  Content-Type: application/wasm
  Content-Encoding: br
  Cache-Control: public, max-age=31536000, immutable

/*.mem.br
  Content-Type: application/octet-stream
  Content-Encoding: br
  Cache-Control: public, max-age=31536000, immutable

# Also cover /Build/ prefixed public paths in case the folder was uploaded differently
/Build/*.js.br
  Content-Type: application/javascript
  Content-Encoding: br

/Build/*.framework.js.br
  Content-Type: application/javascript
  Content-Encoding: br

/Build/*.data.br
  Content-Type: application/octet-stream
  Content-Encoding: br

/Build/*.wasm.br
  Content-Type: application/wasm
  Content-Encoding: br

# Fallback non-compressed types
/*.js
  Content-Type: application/javascript

/*.data
  Content-Type: application/octet-stream

/*.wasm
  Content-Type: application/wasm
'@

$destHeaders = Join-Path $BuildPath "_headers"

if ((Test-Path $destHeaders) -and (-not $Force)) {
    Write-Host "A _headers file already exists at $destHeaders. Use -Force to overwrite." -ForegroundColor Yellow
    exit 0
}

# Write UTF8 without BOM to avoid BOM-related parsing problems
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($destHeaders, $headersContent, $utf8NoBom)

if ($?) {
    Write-Host "_headers installed to $destHeaders" -ForegroundColor Green
    Write-Host "Now drag & drop the $BuildPath folder into Netlify (Deploys -> Drag and drop)" -ForegroundColor Cyan
    exit 0
} else {
    Write-Error "Failed to write _headers to $destHeaders"
    exit 1
}
