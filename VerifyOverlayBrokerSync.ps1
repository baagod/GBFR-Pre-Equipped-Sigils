param(
    [Parameter(Mandatory = $true)]
    [string]$OtherRepository
)

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$otherRoot = [System.IO.Path]::GetFullPath($OtherRepository)
$sharedFiles = @(
    [pscustomobject]@{ Path = 'GBFR.OverlayHub.Contracts/OverlayBroker.cs'; Other = @('GBFR.OverlayHub.Contracts/OverlayBroker.cs') },
    [pscustomobject]@{ Path = 'GBFR.OverlayHub.Contracts/OverlayBrokerCapabilities.cs'; Other = @('GBFR.OverlayHub.Contracts/OverlayBrokerCapabilities.cs') },
    [pscustomobject]@{ Path = 'GBFR.OverlayHub.Contracts/OverlayHubContracts.cs'; Other = @('GBFR.OverlayHub.Contracts/OverlayHubContracts.cs') },
    [pscustomobject]@{ Path = 'GBFR.ExtraSigilSlots.Reloaded/OverlayBroker/OverlayBrokerElection.cs'; Other = @('OverlayBroker/OverlayBrokerElection.cs', 'GBFR.ExtraSigilSlots.Reloaded/OverlayBroker/OverlayBrokerElection.cs') },
    [pscustomobject]@{ Path = 'GBFR.ExtraSigilSlots.Reloaded/OverlayBroker/OverlayBrokerHost.cs'; Other = @('OverlayBroker/OverlayBrokerHost.cs', 'GBFR.ExtraSigilSlots.Reloaded/OverlayBroker/OverlayBrokerHost.cs') },
    [pscustomobject]@{ Path = 'GBFR.ExtraSigilSlots.Reloaded/OverlayBroker/OverlayWindowInputClassifier.cs'; Other = @('OverlayBroker/OverlayWindowInputClassifier.cs', 'GBFR.ExtraSigilSlots.Reloaded/OverlayBroker/OverlayWindowInputClassifier.cs') },
    [pscustomobject]@{ Path = 'GBFR.ExtraSigilSlots.Reloaded/OverlayBroker/SharedImguiGraphicsBinding.cs'; Other = @('OverlayBroker/SharedImguiGraphicsBinding.cs', 'GBFR.ExtraSigilSlots.Reloaded/OverlayBroker/SharedImguiGraphicsBinding.cs') }
)

$differences = @()
foreach ($entry in $sharedFiles) {
    $localPath = Join-Path $repositoryRoot $entry.Path
    $otherPath = $entry.Other |
        ForEach-Object { Join-Path $otherRoot $_ } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    if (-not (Test-Path -LiteralPath $localPath -PathType Leaf) -or -not $otherPath) {
        $differences += "$($entry.Path) (missing)"
        continue
    }

    $localHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $localPath).Hash
    $otherHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $otherPath).Hash
    if ($localHash -ne $otherHash) {
        $differences += "$($entry.Path) (different)"
    }
}

if ($differences.Count -ne 0) {
    Write-Error ("Overlay Broker shared files are out of sync:`n" + ($differences -join "`n"))
    exit 1
}

Write-Host "Overlay Broker shared files match $otherRoot"
