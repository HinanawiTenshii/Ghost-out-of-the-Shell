# Source regression guard; does not replace a Unity Play Mode interaction test.
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..'
$cases = @(
    @('Assets/Scripts/Interaction/DoorHingeInteraction.cs', 'private void UpdateInteractionPrompt()', 'EnsureInteractionPrompt();'),
    @('Assets/Scripts/Interaction/LeverData.cs', 'private void UpdateInteractionPrompt()', 'EnsureInteractionPrompt();'),
    @('Assets/Scripts/Interaction/SpecificItemSubmissionStation.cs', 'private void UpdateInteractionPrompt()', 'EnsureInteractionPrompt();'),
    @('Assets/Scripts/Items/PickupItemBase.cs', 'private void UpdatePickupPrompt(', 'EnsurePickupPrompt();'),
    @('Assets/Scripts/SceneManagement/SceneTransitionPoint.cs', 'private void LateUpdate()', 'EnsureInteractionPrompt();'),
    @('Assets/Scripts/Items/ClockworkPuppetRuntime.cs', 'private void TryOfferReclaim()', 'EnsureReclaimPrompt();')
)
foreach ($case in $cases) {
    $source = Get-Content -Raw -LiteralPath (Join-Path $root $case[0])
    $start = $source.IndexOf($case[1], [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Method missing: $($case[0]) $($case[1])" }
    $open = $source.IndexOf('{', $start)
    $depth = 1
    $end = $open + 1
    while ($end -lt $source.Length -and $depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    $body = $source.Substring($open, $end - $open)
    $offer = $body.IndexOf('ZeldaInteractionArbiter.OfferInteraction(')
    $visual = $body.IndexOf($case[2])
    if ($offer -lt 0 -or $visual -lt 0 -or $offer -gt $visual) {
        throw "Gameplay registration still depends on prompt creation: $($case[0])"
    }
    if ([regex]::Matches($body, 'ZeldaInteractionArbiter\.OfferInteraction\(').Count -ne 1) {
        throw "Expected exactly one offer per candidate: $($case[0])"
    }
    Write-Output "PASS: offer precedes optional prompt - $($case[0])"
}
