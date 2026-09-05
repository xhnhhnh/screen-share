# 编译屏幕共享桌面端（共享/观看二合一）：ScreenShare.exe
# 仅需系统自带 csc(.NET Framework 4.8)，无需安装 SDK
$ErrorActionPreference = "Stop"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"

if (-not (Test-Path "bin")) { New-Item -ItemType Directory -Path "bin" | Out-Null }
Push-Location $PSScriptRoot
try {
    $common = @(
        "/nologo", "/optimize+", "/target:winexe", "/platform:anycpu",
        "/win32manifest:app.manifest",
        "/r:$fw\System.dll",
        "/r:$fw\System.Drawing.dll",
        "/r:$fw\System.Windows.Forms.dll",
        "/resource:Enable-ThunderboltBridge.ps1,ScreenShare.Bridge.ps1"
    )
    & $csc @common "/out:bin\ScreenShare.exe" "App.cs" "HostEngine.cs" "ScreenShareForm.cs" "FluentUI.cs" "BridgeConfigurer.cs"
    if ($LASTEXITCODE -ne 0) { throw "编译失败" }
    Write-Host "OK: bin\ScreenShare.exe"
} finally { Pop-Location }
