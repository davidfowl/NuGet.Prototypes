$scriptPath = split-path $MyInvocation.MyCommand.Path -parent
$artifacts = Join-Path (Join-Path $scriptPath "artifacts") "packages"
$ts = [DateTime]::UtcNow.Ticks.ToString()
$env:K_BUILD_VERSION="t$ts"
ls -r project.json | %{ kpm build $_; kpm packages add (Join-Path (Join-Path $_.Directory "bin\Debug") "$($_.Directory.Name).1.0.0-t$ts.nupkg") $artifacts }