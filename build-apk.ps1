# Build APK for sideloading
# Creates a signed APK that can be shared and installed on any Android device

Write-Host "=== Building APK for Sideloading ===" -ForegroundColor Cyan

# Add Android SDK tools to PATH
$adbDir = "$env:LOCALAPPDATA\Android\Sdk\platform-tools"
if (Test-Path $adbDir) {
    $env:PATH = "$adbDir;$env:PATH"
}

Set-Location $PSScriptRoot

# Build Release APK
Write-Host "`n[1/2] Building Release APK..." -ForegroundColor Yellow
dotnet publish NoPasaranFC.Android\NoPasaranFC.Android.csproj -c Release -f net9.0-android

$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "`n=== Build Successful! ===" -ForegroundColor Green
    
    # Find the APK
    $apkPath = Get-ChildItem -Path "NoPasaranFC.Android\bin\Release\net9.0-android\publish" -Filter "*.apk" -ErrorAction SilentlyContinue | Select-Object -First 1
    
    if ($apkPath) {
        Write-Host "`nAPK created at:" -ForegroundColor Cyan
        Write-Host "  $($apkPath.FullName)" -ForegroundColor White
        
        # Copy to project root for easy access
        $destPath = Join-Path $PSScriptRoot "NoPasaranFC.apk"
        Copy-Item $apkPath.FullName $destPath -Force
        Write-Host "`nCopied to:" -ForegroundColor Cyan
        Write-Host "  $destPath" -ForegroundColor White
        
        $size = [math]::Round($apkPath.Length / 1MB, 2)
        Write-Host "`nAPK Size: $size MB" -ForegroundColor Green
    } else {
        # Check for signed APK in different location
        $signedApk = Get-ChildItem -Path "NoPasaranFC.Android\bin\Release\net9.0-android" -Filter "*-Signed.apk" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($signedApk) {
            Write-Host "`nSigned APK created at:" -ForegroundColor Cyan
            Write-Host "  $($signedApk.FullName)" -ForegroundColor White
            
            $destPath = Join-Path $PSScriptRoot "NoPasaranFC.apk"
            Copy-Item $signedApk.FullName $destPath -Force
            Write-Host "`nCopied to:" -ForegroundColor Cyan
            Write-Host "  $destPath" -ForegroundColor White
        } else {
            Write-Host "`nLooking for APK files..." -ForegroundColor Yellow
            Get-ChildItem -Path "NoPasaranFC.Android\bin\Release" -Filter "*.apk" -Recurse | ForEach-Object {
                Write-Host "  Found: $($_.FullName)"
            }
        }
    }
    
    Write-Host "`n--- Installation Instructions ---" -ForegroundColor Cyan
    Write-Host "1. Transfer the APK to your Android device"
    Write-Host "2. On the device, enable 'Install from unknown sources' in Settings"
    Write-Host "3. Open the APK file to install"
    Write-Host ""
} else {
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
}

exit $exitCode
