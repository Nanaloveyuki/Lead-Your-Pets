[CmdletBinding()]
param(
    [string]$RimWorldDir = 'D:\Appdata\Steam\steamapps\common\RimWorld',
    [string]$MouseDisasterAssembly = 'F:\repo\Ratkin-Great-Famine-Year-Continued\1.6\Assemblies\MouseDisaster.dll'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$managed = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = [Reflection.AssemblyName]::new($eventArgs.Name).Name + '.dll'
    foreach ($dir in @($managed, (Join-Path $root 'Assemblies'))) {
        $path = Join-Path $dir $name
        if (Test-Path $path) { return [Reflection.Assembly]::LoadFrom($path) }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $mouse = [Reflection.Assembly]::LoadFrom($MouseDisasterAssembly)
    $pets = [Reflection.Assembly]::LoadFrom((Join-Path $root 'Assemblies/LeadYourPet.dll'))
    $utility = $pets.GetType('LeadYourPet.LeadYourPetUtility', $true)
    [Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($utility.TypeHandle)
    $flags = [Reflection.BindingFlags]'NonPublic,Static'
    foreach ($name in @('MouseDisasterGenerateFactionRatkinPawnMethod','MouseDisasterSetBiologicalAgeYearsMethod','MouseDisasterPrepareTradablePrisonerMethod','MouseDisasterClearTradeLeaderStateMethod','MouseDisasterResetBeggarStateMethod','MouseDisasterMakeFactionHostileToPlayerMethod','MouseDisasterTryRefreshHiddenFactionRelationsMethod','MouseDisasterNotifyIdentityChangedMethod','MouseDisasterTryMakeHostileVisitorsMethod')) {
        $method = $utility.GetField($name, $flags).GetValue($null)
        if ($null -eq $method) { throw "Unresolved integration: $name" }
        Write-Host "$name -> $($method.DeclaringType.FullName).$method"
    }
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    $pawn = $game.GetType('Verse.Pawn', $true)
    $component = $pets.GetType('LeadYourPet.LeadYourPetGameComponent', $true)
    $signatures = @{
        TryStartRatkinMotherLeash = [Type[]]@($pawn,$pawn,[bool])
        StartLeash = [Type[]]@($pawn,$pawn,[bool],[bool])
        TryAssignTravelMouseEggs = [Type[]]@($game.GetType('Verse.AI.Group.Lord', $true))
        AnchorLeashedPetsToCell = [Type[]]@($pawn,$game.GetType('Verse.IntVec3', $true))
        EndLeashForPet = [Type[]]@($pawn,[bool])
    }
    foreach ($name in $signatures.Keys) {
        if ($null -eq $component.GetMethod($name, $signatures[$name])) { throw "Unresolved reverse integration: $name" }
    }
    Write-Host 'LeadYourPetUtility initialized against real MouseDisaster DLL; 9 forward bindings and 5 reverse signatures verified. No game simulation performed.'
} finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
