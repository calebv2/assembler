param(
 [string]$Package=(Join-Path $PSScriptRoot '..\..\dist\Native'),
 [Parameter(Mandatory=$true)][string]$GamePath,
 [string]$Bundle=(Join-Path $PSScriptRoot '..\..\Builds\AssemblerModel\arrowassembler-model')
)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $GamePath 'MelonLoader\net35\Mono.Cecil.dll')
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GamePath 'A Township Tale_Data\Managed\Root.Township.dll'))
try {
 $contracts=@{
 'Alta.Blacksmithing.Smelter'=@('oreDocks');
 'Alta.Inventory.PickupDock'=@('dockVisual','settings','pickupRange','hasContentsRange');
 'Alta.Inventory.AutomaticDockArea'=@('colliders');
 'Alta.Networking.NetworkEntity'=@('hash');
 'Alta.Inventory.PickupDockSettings'=@('maxContainedStacks','isMultiStackingAllowed','isStackingDisabled','isStackingEnforced','isAllowingCraftedItems','isAllowingSubDocking','isHandDockingAllowed','isGrabbable','allowContainerInteraction','minimumCraftingSize','maximumCraftingSize')
 }
 foreach($name in $contracts.Keys) {
  $type=$game.MainModule.Types | Where-Object FullName -eq $name
  if(!$type){throw "Missing native type: $name"}
  foreach($field in $contracts[$name]){if($field -notin $type.Fields.Name){throw "Missing native field: $name.$field"}}
 }
 'PASS: all reflected native fields exist in installed game.'
} finally {$game.Dispose()}
$sha=[Security.Cryptography.SHA256]::Create()
try {
 $expected=[Convert]::ToBase64String($sha.ComputeHash([IO.File]::ReadAllBytes($Bundle)))
 foreach($role in @('Client','Server')) {
  $a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $Package "$role\ArrowAssembler.dll"))
  try {
   $info=$a.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'MelonLoader.MelonInfoAttribute'}
   if($info.ConstructorArguments[1].Value -ne 'ArrowAssembler' -or $info.ConstructorArguments[2].Value -ne '0.1.13' -or $info.ConstructorArguments[3].Value -ne 'caleb'){throw 'Mod identity mismatch'}
   $m=$a.MainModule.Types | Where-Object FullName -eq 'ArrowAssemblerNative.AssemblerMachine'
   if($m.BaseType.FullName -ne 'Alta.Networking.NetworkEntityBehaviour'){throw 'Missing network behaviour'}
   if(('Assemble' -in $m.Methods.Name) -ne ($role -eq 'Server')){throw 'Incorrect crafting role isolation'}
   $r=$a.MainModule.Resources | Where-Object Name -eq 'ArrowAssembler.Model'
   if(!$r -or [Convert]::ToBase64String($sha.ComputeHash($r.GetResourceData())) -ne $expected){throw 'Embedded bundle differs from approved model'}
   foreach($dependency in @('SyncLib','Root.Township','MelonLoader')){if($dependency -notin $a.MainModule.AssemblyReferences.Name){throw "Missing dependency: $dependency"}}
   "PASS: $role identity, native dependencies, embedded model, and server-only assembly logic."
  } finally {$a.Dispose()}
 }
} finally {$sha.Dispose()}
'NOT TESTED: live prefab registration, donor hierarchy, recipe discovery, VR, multiplayer, save/reconnect.'
'Package is an integration candidate, not a certified in-game release.'




