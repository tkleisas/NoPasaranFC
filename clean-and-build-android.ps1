# Clean and Build Android Script
# Fixes the SQLite native library locking issue

Write-Host "=== Android Build Cleanup Script ===" -ForegroundColor Cyan

# Step 0: Add Android SDK tools to PATH
$adbDir = "$env:LOCALAPPDATA\Android\Sdk\platform-tools"
if (Test-Path $adbDir) {
    $env:PATH = "$adbDir;$env:PATH"
    Write-Host "Added Android SDK platform-tools to PATH" -ForegroundColor Green
}

# Step 1: Find and kill all dotnet processes
Write-Host "`n[1/4] Stopping dotnet processes..." -ForegroundColor Yellow
$dotnetProcs = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
if ($dotnetProcs) {
    foreach ($proc in $dotnetProcs) {
        Write-Host "  Killing dotnet process ID: $($proc.Id)"
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 2
} else {
    Write-Host "  No dotnet processes found"
}

# Step 2: Find and kill any adb processes (if running)
Write-Host "`n[2/4] Stopping adb processes..." -ForegroundColor Yellow
$adbProcs = Get-Process -Name "adb" -ErrorAction SilentlyContinue
if ($adbProcs) {
    foreach ($proc in $adbProcs) {
        Write-Host "  Killing adb process ID: $($proc.Id)"
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
} else {
    Write-Host "  No adb processes found (this is OK)"
}

# Also check for any process locking the sqlite file
Write-Host "`n  Checking for processes locking SQLite files..." -ForegroundColor Yellow
$sqliteProcs = Get-Process | Where-Object { $_.Modules.FileName -like "*sqlite*" } -ErrorAction SilentlyContinue
if ($sqliteProcs) {
    foreach ($proc in $sqliteProcs) {
        if ($proc.Name -ne "powershell" -and $proc.Name -ne "pwsh") {
            Write-Host "  Found process using SQLite: $($proc.Name) (ID: $($proc.Id))"
        }
    }
}

# Step 3: Clean obj and bin folders
Write-Host "`n[3/4] Cleaning build folders..." -ForegroundColor Yellow
$androidProjectPath = Join-Path $PSScriptRoot "NoPasaranFC.Android"

$objPath = Join-Path $androidProjectPath "obj"
$binPath = Join-Path $androidProjectPath "bin"

if (Test-Path $objPath) {
    Write-Host "  Removing: $objPath"
    Remove-Item -Path $objPath -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path $binPath) {
    Write-Host "  Removing: $binPath"
    Remove-Item -Path $binPath -Recurse -Force -ErrorAction SilentlyContinue
}

# Also clean the locked file specifically if it still exists
$lockedFile = Join-Path $androidProjectPath "obj\Debug\net9.0-android\lp\3\jl\jni\arm64-v8a\libe_sqlite3.so"
if (Test-Path $lockedFile) {
    Write-Host "  Force removing locked SQLite file..."
    Remove-Item -Path $lockedFile -Force -ErrorAction SilentlyContinue
}

Write-Host "  Done cleaning"

# Step 4: Build
Write-Host "`n[4/4] Building Android project..." -ForegroundColor Yellow
Set-Location $PSScriptRoot

# Run build
dotnet build NoPasaranFC.Android\NoPasaranFC.Android.csproj -c Debug

$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "`n=== Build Successful! ===" -ForegroundColor Green
    Write-Host "`nDeploying to device..." -ForegroundColor Cyan
    dotnet build NoPasaranFC.Android\NoPasaranFC.Android.csproj -t:Install -c Debug
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n=== Deployed Successfully! ===" -ForegroundColor Green
    } else {
        Write-Host "`n=== Deployment Failed ===" -ForegroundColor Red
    }
} else {
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
}

exit $exitCode
