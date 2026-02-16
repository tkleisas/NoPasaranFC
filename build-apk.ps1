# Build APK for sideloading
# Creates a signed APK that can be shared and installed on any Android device

Write-Host "=== Building APK for Sideloading ===" -ForegroundColor Cyan

# Add Android SDK tools to PATH
$adbDir = "$env:LOCALAPPDATA\Android\Sdk\platform-tools"
if (Test-Path $adbDir) {
    $env:PATH = "$adbDir;$env:PATH"
}

Set-Location $PSScriptRoot

# Step 1: Build content for Android platform
Write-Host "`n[1/3] Building content for Android platform..." -ForegroundColor Yellow

$contentDir = Join-Path $PSScriptRoot "Content"

# Use dotnet-mgcb tool to rebuild content targeting Android
# This produces .m4a song files and Android-compatible assets under Content\bin\Android\
Push-Location $contentDir
try {
    dotnet mgcb Content.mgcb /platform:Android /rebuild
    $contentExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

if ($contentExitCode -ne 0) {
    Write-Host "`n=== Content Build Failed ===" -ForegroundColor Red
    Write-Host "Make sure the MonoGame Content Builder tool is installed:" -ForegroundColor Yellow
    Write-Host "  dotnet tool install -g dotnet-mgcb" -ForegroundColor White
    exit $contentExitCode
}

Write-Host "Content built successfully for Android." -ForegroundColor Green

# Step 2: Build Release APK
Write-Host "`n[2/3] Building Release APK..." -ForegroundColor Yellow
dotnet publish NoPasaranFC.Android\NoPasaranFC.Android.csproj -c Release -f net9.0-android

$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "`n=== Build Successful! ===" -ForegroundColor Green
    
    # Step 3: Locate and copy APK
    Write-Host "`n[3/3] Locating APK..." -ForegroundColor Yellow
    
    # Find the APK
    $apkFile = $null
    $apkPath = Get-ChildItem -Path "NoPasaranFC.Android\bin\Release\net9.0-android\publish" -Filter "*.apk" -ErrorAction SilentlyContinue | Select-Object -First 1
    
    if ($apkPath) {
        $apkFile = $apkPath
    } else {
        # Check for signed APK in different location
        $signedApk = Get-ChildItem -Path "NoPasaranFC.Android\bin\Release\net9.0-android" -Filter "*-Signed.apk" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($signedApk) {
            $apkFile = $signedApk
        } else {
            Write-Host "`nLooking for APK files..." -ForegroundColor Yellow
            Get-ChildItem -Path "NoPasaranFC.Android\bin\Release" -Filter "*.apk" -Recurse | ForEach-Object {
                Write-Host "  Found: $($_.FullName)"
            }
        }
    }
    
    if ($apkFile) {
        Write-Host "`nAPK created at:" -ForegroundColor Cyan
        Write-Host "  $($apkFile.FullName)" -ForegroundColor White
        
        # Copy to project root for easy access
        $destPath = Join-Path $PSScriptRoot "NoPasaranFC.apk"
        Copy-Item $apkFile.FullName $destPath -Force
        Write-Host "`nCopied to:" -ForegroundColor Cyan
        Write-Host "  $destPath" -ForegroundColor White
        
        $size = [math]::Round($apkFile.Length / 1MB, 2)
        Write-Host "`nAPK Size: $size MB" -ForegroundColor Green
        
        # Ask user if they want to deploy to a connected device
        Write-Host ""
        $deploy = Read-Host "Deploy to connected Android device? (y/N)"
        
        if ($deploy -eq 'y' -or $deploy -eq 'Y') {
            # Check if adb is available
            $adbCmd = Get-Command adb -ErrorAction SilentlyContinue
            if (-not $adbCmd) {
                Write-Host "`nadb not found. Make sure Android SDK platform-tools are installed and in PATH." -ForegroundColor Red
                Write-Host "  Expected location: $adbDir" -ForegroundColor Yellow
            } else {
                # Check for connected devices
                $devices = adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\S' }
                
                if (-not $devices) {
                    Write-Host "`nNo Android devices found." -ForegroundColor Red
                    Write-Host "Make sure your device is connected via USB with USB debugging enabled." -ForegroundColor Yellow
                } else {
                    Write-Host "`nConnected devices:" -ForegroundColor Cyan
                    $devices | ForEach-Object { Write-Host "  $_" -ForegroundColor White }
                    
                    Write-Host "`nInstalling APK..." -ForegroundColor Yellow
                    adb install -r $destPath
                    
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "`nInstalled successfully!" -ForegroundColor Green
                        
                        $launch = Read-Host "Launch the app? (y/N)"
                        if ($launch -eq 'y' -or $launch -eq 'Y') {
                            adb shell am start -n com.nopasaranfc.game/crc64e20e0e16d2616218.Activity1
                            if ($LASTEXITCODE -eq 0) {
                                Write-Host "App launched!" -ForegroundColor Green
                            } else {
                                # Fallback: use monkey to launch by package name
                                Write-Host "Trying alternative launch method..." -ForegroundColor Yellow
                                adb shell monkey -p com.nopasaranfc.game -c android.intent.category.LAUNCHER 1 2>$null
                                if ($LASTEXITCODE -eq 0) {
                                    Write-Host "App launched!" -ForegroundColor Green
                                } else {
                                    Write-Host "Could not launch automatically. Open the app manually on your device." -ForegroundColor Yellow
                                }
                            }
                        }
                    } else {
                        Write-Host "`nInstallation failed." -ForegroundColor Red
                    }
                }
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
