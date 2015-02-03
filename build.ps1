build\kvm.ps1 upgrade
$scriptPath = split-path $MyInvocation.MyCommand.Path -parent
$artifacts = Join-Path $scriptPath "artifacts"
$nuget3Output = Join-Path $artifacts "nuget3"
$packageOutput = Join-Path $artifacts "packages"
$ts = [DateTime]::UtcNow.Ticks.ToString()
$env:K_BUILD_VERSION="t$ts"
mkdir $artifacts -Force | Out-Null
ls -r project.json | ?{ $_.Directory.Name -ne "NuGet3" } | %{ kpm pack $_; cp (Join-Path (Join-Path $_.Directory "bin\Debug") "$($_.Directory.Name).1.0.0-t$ts.nupkg") $packageOutput }
kpm bundle --no-source src\NuGet3 --out $nuget3Output