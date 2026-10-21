param(
 [Parameter(Mandatory=$true)][string]$GamePath,
 [Parameter(Mandatory=$true)][string]$SyncLib,
 [string]$UnityPath="$env:ProgramFiles\Unity\Hub\Editor\2020.3.23f1\Editor\Unity.exe",
 [string]$Bundle=(Join-Path $PSScriptRoot '..\..\Builds\AssemblerModel\arrowassembler-model'),
 [string]$Output=(Join-Path $PSScriptRoot '..\..\dist\Native')
)
$ErrorActionPreference='Stop'
$compiler=Join-Path (Split-Path $UnityPath) 'Data\Tools\Roslyn\csc.exe'
foreach($path in @($compiler,$SyncLib,$Bundle)){if(!(Test-Path -LiteralPath $path)){throw "Missing input: $path"}}
foreach($role in @('Client','Server')) {
 $folder=Join-Path $Output $role
 New-Item -ItemType Directory -Force -Path $folder | Out-Null
 $arguments=@('/nologo','/target:library','/langversion:8','/nostdlib+','/warnaserror+','/optimize+',('/define:'+$role.ToUpperInvariant()))
 $arguments+='/out:"'+(Join-Path $folder 'ArrowAssembler.dll')+'"'
 $arguments+=Get-ChildItem -LiteralPath (Join-Path $GamePath 'A Township Tale_Data\Managed') -Filter '*.dll' | ForEach-Object {'/reference:"'+$_.FullName+'"'}
 foreach($ref in @((Join-Path $GamePath 'MelonLoader\net35\MelonLoader.dll'),(Join-Path $GamePath 'MelonLoader\net35\0Harmony.dll'),$SyncLib)){$arguments+='/reference:"'+$ref+'"'}
 $arguments+='/resource:"'+$Bundle+'",ArrowAssembler.Model'
 $arguments+='"'+(Join-Path $PSScriptRoot '..\..\Assets\Assembler\Runtime\MechanicalRhythm.cs')+'"'
 $arguments+=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object {'"'+$_.FullName+'"'}
 $rsp=Join-Path $folder 'build.rsp'
 Set-Content -LiteralPath $rsp -Value $arguments
 & $compiler ('@'+$rsp)
 if($LASTEXITCODE -ne 0){throw "$role compilation failed"}
}
Get-FileHash -Algorithm SHA256 -LiteralPath $SyncLib,$Bundle,(Join-Path $Output 'Client\ArrowAssembler.dll'),(Join-Path $Output 'Server\ArrowAssembler.dll') | Format-List | Out-File (Join-Path $Output 'Build-Hashes.txt')
