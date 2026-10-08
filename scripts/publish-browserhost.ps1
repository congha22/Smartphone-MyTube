param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\BrowserHost\SmartphoneMyTube.BrowserHost.csproj"
$outRoot = Join-Path $PSScriptRoot "..\browserhost"
$rids = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")

foreach ($rid in $rids) {
    Write-Host "Publishing BrowserHost for $rid"
    dotnet publish $project -c $Configuration -r $rid --self-contained true -o (Join-Path $outRoot $rid)
}

Write-Host "NOTE: CEF native runtime/helper packaging still needs to be validated per platform."
