param(
 [Parameter(Mandatory=$true)][string]$GamePath,
 [Parameter(Mandatory=$true)][string]$SyncLib,
 [string]$UnityPath="$env:ProgramFiles\Unity\Hub\Editor\2020.3.23f1\Editor\Unity.exe",
 [string]$Output=(Join-Path $PSScriptRoot 'dist'),
 [switch]$SkipModelExport
)
$ErrorActionPreference='Stop'
foreach($path in @($UnityPath,$SyncLib,(Join-Path $GamePath 'A Township Tale_Data\Managed\Root.Township.dll'))){
 if(!(Test-Path -LiteralPath $path)){throw "Required dependency not found: $path"}
}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$bundle=Join-Path $PSScriptRoot 'Builds\AssemblerModel\arrowassembler-model'
if(!$SkipModelExport){
 $log=Join-Path $Output 'Unity-export.log'
 $arguments=@('-batchmode','-quit','-projectPath',('"'+$PSScriptRoot+'"'),'-executeMethod','Builder.Assembler.Editor.AssemblerExport.Export','-logFile',('"'+$log+'"'))
 $process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
 if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $bundle)){throw "Unity export failed. See $log"}
}
if(!(Test-Path -LiteralPath $bundle)){throw 'Export the model from Unity before using -SkipModelExport.'}
$native=Join-Path $Output 'Native'
& (Join-Path $PSScriptRoot 'NativeMod\Source\Build.ps1') -GamePath $GamePath -SyncLib $SyncLib -UnityPath $UnityPath -Bundle $bundle -Output $native
& (Join-Path $PSScriptRoot 'NativeMod\Source\Verify-Package.ps1') -GamePath $GamePath -Bundle $bundle -Package $native
$version='0.1.14'
$package=Join-Path $Output "Assembler-Mod-$version"
foreach($role in @('Client','Server')){
 $target=Join-Path $package "$role\Mods"
 New-Item -ItemType Directory -Force -Path $target | Out-Null
 Copy-Item -LiteralPath (Join-Path $native "$role\ArrowAssembler.dll") -Destination $target -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\INSTALL.txt') -Destination (Join-Path $package 'INSTALL.txt') -Force
$zip=Join-Path $Output "Assembler-Mod-$version.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $Output 'SHA256SUMS.txt') -Value "$hash  $([IO.Path]::GetFileName($zip))"
Write-Output "Built and packaged: $zip"
