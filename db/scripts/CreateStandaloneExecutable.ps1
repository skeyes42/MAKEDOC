# publish_findSDTTagsApp.ps1
# Purpose: Build and publish FindSDTTagsApp as a single-file executable
# Author: Steven (PowerShell automation enthusiast)
# Date: 2026-05-28

# --- CONFIGURATION SECTION ---
# Path to your project (.csproj)
$projectPath = "C:\Users\skeye\BOOK2\MAKEDOC\db\scripts\FindSDTTagsApp\FindSDTTagsApp.csproj"

# Target runtime (Windows 64-bit)
$runtime = "win-x64"

# Output folder for the published executable
$publishDir = "C:\Users\skeye\BOOK2\MAKEDOC\published"

# --- BUILD AND PUBLISH ---
Write-Host "Publishing FindSDTTagsApp as a single-file executable..." -ForegroundColor Cyan

# Run dotnet publish with self-contained and single-file options
dotnet publish $projectPath -c Release -r $runtime --self-contained true /p:PublishSingleFile=true

# --- COPY OUTPUT ---
# Locate the publish output folder
$sourceDir = Join-Path (Split-Path $projectPath) "bin\Release\net8.0\$runtime\publish"

# Ensure destination exists
if (!(Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir | Out-Null
}

# Copy all published files
Copy-Item "$sourceDir\*" $publishDir -Recurse -Force

Write-Host "✅ Publish complete! Files copied to:" -ForegroundColor Green
Write-Host $publishDir -ForegroundColor Yellow

# --- OPTIONAL: Run the executable ---
$exePath = Join-Path $publishDir "FindSDTTagsApp.exe"
if (Test-Path $exePath) {
    Write-Host "Launching the app..." -ForegroundColor Cyan
    & $exePath
} else {
    Write-Host "Executable not found in publish directory." -ForegroundColor Red
}
