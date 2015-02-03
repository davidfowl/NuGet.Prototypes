param(
  [parameter(Position=0)]
  [string] $Command,
  [string] $Proxy,
  [switch] $Verbosity = $false,
  [alias("p")][switch] $Persistent = $false,
  [alias("f")][switch] $Force = $false,
  [alias("r")][string] $Runtime,

  [alias("arch")][string] $Architecture,
  [switch] $X86 = $false,
  [alias("amd64")][switch] $X64 = $false,

  [alias("w")][switch] $Wait = $false,
  [alias("a")]
  [string] $Alias = $null,
  [switch] $NoNative = $false,
  [parameter(Position=1, ValueFromRemainingArguments=$true)]
  [string[]]$Args=@(),
  [switch] $Quiet,
  [string] $OutputVariable,
  [switch] $AssumeElevated
)

# Constants
Set-Variable -Option Constant "BuildNumber" "10308"
Set-Variable -Option Constant "RuntimePackageName" "kre"
Set-Variable -Option Constant "RuntimeFriendlyName" "K Runtime"
Set-Variable -Option Constant "RuntimeShortName" "KRE"
Set-Variable -Option Constant "RuntimeFolderName" ".k"
Set-Variable -Option Constant "CommandName" "kvm"
Set-Variable -Option Constant "VersionManagerName" "K Version Manager"
Set-Variable -Option Constant "DefaultFeed" "https://www.myget.org/F/aspnetvnext/api/v2"
Set-Variable -Option Constant "CrossGenCommand" "k-crossgen"
Set-Variable -Option Constant "HomeEnvVar" "KRE_HOME"
Set-Variable -Option Constant "UserHomeEnvVar" "KRE_USER_HOME"
Set-Variable -Option Constant "FeedEnvVar" "KRE_FEED"


$selectedArch=$null;
$defaultArch="x86"
$selectedRuntime=$null
$defaultRuntime="clr"

function getenv($name) {
  if(Test-Path "env:\$name") {
    cat "env:\$name"
  }
}

# Get or calculate userHome
$userHome = (getenv $UserHomeEnvVar)
if(!$userHome) { $userHome = $env:USERPROFILE + "\$RuntimeFolderName" }
$userRuntimesPath = $userHome + "\runtimes"

# Get the feed from the environment variable or set it to the default value
$feed = (getenv $FeedEnvVar)
if (!$feed)
{
    $feed = $DefaultFeed;
}
$feed = $feed.TrimEnd("/")

# In some environments, like Azure Websites, the Write-* cmdlets don't work
$useHostOutputMethods = $true

function String-IsEmptyOrWhitespace([string]$str) {
     return [string]::IsNullOrEmpty($str) -or $str.Trim().length -eq 0
}

$scriptPath = $myInvocation.MyCommand.Definition

function _Help {
@"
$VersionManagerName - Build $BuildNumber

USAGE: $CommandName <command> [options]

$CommandName upgrade [-X86|-X64] [-r|-Runtime CLR|CoreCLR] [-g|-Global] [-f|-Force] [-Proxy <ADDRESS>] [-NoNative]
  install latest $RuntimeShortName from feed
  set 'default' alias to installed version
  add $RuntimeShortName bin to user PATH environment variable
  -g|-Global        install to machine-wide location
  -f|-Force         upgrade even if latest is already installed
  -Proxy <ADDRESS>  use given address as proxy when accessing remote server (e.g. https://username:password@proxyserver:8080/). Alternatively set proxy using http_proxy environment variable.
  -NoNative         Do not generate native images (Effective only for CoreCLR flavors)

$CommandName install <semver>|<alias>|<nupkg>|latest [-X86|-X64] [-r|-Runtime CLR|CoreCLR] [-a|-Alias <alias>] [-f|-Force] [-Proxy <ADDRESS>] [-NoNative]
  <semver>|<alias>  install requested $RuntimeShortName from feed
  <nupkg>           install requested $RuntimeShortName from package on local filesystem
  latest            install latest $RuntimeShortName from feed
  add $RuntimeShortName bin to path of current command line
  -p|-Persistent    add $RuntimeShortName bin to PATH environment variables persistently
  -a|-Alias <alias> set alias <alias> for requested $RuntimeShortName on install
  -f|-Force         install even if specified version is already installed
  -Proxy <ADDRESS>  use given address as proxy when accessing remote server (e.g. https://username:password@proxyserver:8080/). Alternatively set proxy using http_proxy environment variable.
  -NoNative         Do not generate native images (Effective only for CoreCLR flavors)

$CommandName use <semver>|<alias>|<package>|none [-X86|-X64] [-r|-Runtime CLR|CoreCLR] [-p|-Persistent]
  <semver>|<alias>|<package>  add $RuntimeShortName bin to path of current command line
  none                        remove $RuntimeShortName bin from path of current command line
  -p|-Persistent              add $RuntimeShortName bin to PATH environment variable across all processes run by the current user

$CommandName list
  list $RuntimeShortName versions installed

$CommandName alias
  list $RuntimeShortName aliases which have been defined

$CommandName alias <alias>
  display value of the specified alias

$CommandName alias <alias> <semver>|<alias>|<package> [-X86|-X64] [-r|-Runtime CLR|CoreCLR]
  <alias>                      the name of the alias to set
  <semver>|<alias>|<package>   the $RuntimeShortName version to set the alias to. Alternatively use the version of the specified alias

$CommandName unalias <alias>
  remove the specified alias

"@ -replace "`n","`r`n" | Console-Write
}

function _Global-Setup {
  # Sets up the version manager tool and adds the user-local runtime install directory to the home variable
  # Note: We no longer do global install via this tool. The MSI handles global install of runtimes AND will set
  # the machine level home value.

  # In this configuration, the user-level path will OVERRIDE the global path because it is placed first.

  $cmdBinPath = "$userHome\bin"

  If (Needs-Elevation)
  {
    $arguments = "-ExecutionPolicy unrestricted & '$scriptPath' setup -wait"
    Start-Process "$psHome\powershell.exe" -Verb runAs -ArgumentList $arguments -Wait
    Console-Write "Adding $cmdBinPath to process PATH"
    Set-Path (Change-Path $env:Path $cmdBinPath ($cmdBinPath))
    Console-Write "Adding %USERPROFILE%\$RuntimeFolderName to process $HomeEnvVar"
    $envRuntimeHome = (getenv $HomeEnvVar)
    $envRuntimeHome = Change-Path $envRuntimeHome "%USERPROFILE%\$RuntimeFolderName" ("%USERPROFILE%\$RuntimeFolderName")
    Set-Content "env:\$HomeEnvVar" $envRuntimeHome
    Console-Write "Setup complete"
    break
  }

  $scriptFolder = [System.IO.Path]::GetDirectoryName($scriptPath)

  Console-Write "Copying file $cmdBinPath\$CommandName.ps1"
  md $cmdBinPath -Force | Out-Null
  copy "$scriptFolder\$CommandName.ps1" "$cmdBinPath\$CommandName.ps1"

  Console-Write "Copying file $cmdBinPath\$CommandName.cmd"
  copy "$scriptFolder\$CommandName.cmd" "$cmdBinPath\$CommandName.cmd"

  Console-Write "Adding $cmdBinPath to process PATH"
  Set-Path (Change-Path $env:Path $cmdBinPath ($cmdBinPath))

  Console-Write "Adding $cmdBinPath to user PATH"
  $userPath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
  $userPath = Change-Path $userPath $cmdBinPath ($cmdBinPath)
  [Environment]::SetEnvironmentVariable("Path", $userPath, [System.EnvironmentVariableTarget]::User)

  Console-Write "Adding %USERPROFILE%\$RuntimeFolderName to process $HomeEnvVar"
  $envRuntimeHome = (getenv $HomeEnvVar)
  $envRuntimeHome = Change-Path $envRuntimeHome "%USERPROFILE%\$RuntimeFolderName" ("%USERPROFILE%\$RuntimeFolderName")
  Set-Content "env:\$HomeEnvVar" $envRuntimeHome

  Console-Write "Adding %USERPROFILE%\$RuntimeFolderName to machine $HomeEnvVar"
  $machineruntimeHome = [Environment]::GetEnvironmentVariable($HomeEnvVar, [System.EnvironmentVariableTarget]::Machine)
  $machineruntimeHome = Change-Path $machineruntimeHome "%USERPROFILE%\$RuntimeFolderName" ("%USERPROFILE%\$RuntimeFolderName")
  [Environment]::SetEnvironmentVariable($HomeEnvVar, $machineruntimeHome, [System.EnvironmentVariableTarget]::Machine)
}

function _Upgrade {
param(
  [boolean] $isGlobal
)
  $Persistent = $true
  $Alias="default"
  _Install "latest" $isGlobal
}

function Add-Proxy-If-Specified {
param(
  [System.Net.WebClient] $wc
)
  if (!$Proxy) {
    $Proxy = $env:http_proxy
  }
  if ($Proxy) {
    $wp = New-Object System.Net.WebProxy($Proxy)
    $pb = New-Object UriBuilder($Proxy)
    if (!$pb.UserName) {
        $wp.Credentials = [System.Net.CredentialCache]::DefaultCredentials
    } else {
        $wp.Credentials = New-Object System.Net.NetworkCredential($pb.UserName, $pb.Password)
    }
    $wc.Proxy = $wp
  }
}

function _Find-Latest {
param(
  [string] $platform,
  [string] $architecture
)
  Console-Write "Determining latest version"

  $url = "$feed/GetUpdates()?packageIds=%27$RuntimePackageName-$platform-win-$architecture%27&versions=%270.0%27&includePrerelease=true&includeAllVersions=false"

  $wc = New-Object System.Net.WebClient
  Add-Proxy-If-Specified($wc)
  Write-Verbose "Downloading $url ..."
  [xml]$xml = $wc.DownloadString($url)

  $version = Select-Xml "//d:Version" -Namespace @{d='http://schemas.microsoft.com/ado/2007/08/dataservices'} $xml

  if (String-IsEmptyOrWhitespace($version)) {
    throw "There are no runtimes for platform '$platform', architecture '$architecture' in the feed '$feed'"
  }

  return $version
}

function Do-Download {
param(
  [string] $runtimeFullName,
  [string] $runtimesFolder
)
  $parts = $runtimeFullName.Split(".", 2)

  $url = "$feed/package/" + $parts[0] + "/" + $parts[1]
  $runtimeFolder = Join-Path $runtimesFolder $runtimeFullName
  $runtimeFile = Join-Path $runtimeFolder "$runtimeFullName.nupkg"

  If (Test-Path $runtimeFolder) {
    if($Force)
    {
      rm $runtimeFolder -Recurse -Force
    } else {
      Console-Write "$runtimeFullName already installed."
      return;
    }
  }

  Console-Write "Downloading $runtimeFullName from $feed"

  #Downloading to temp location
  $runtimeTempDownload = Join-Path $runtimesFolder "temp"
  $tempDownloadFile = Join-Path $runtimeTempDownload "$runtimeFullName.nupkg"

  if(Test-Path $runtimeTempDownload) {
    del "$runtimeTempDownload\*" -recurse
  } else {
    md $runtimeTempDownload -Force | Out-Null
  }

  $wc = New-Object System.Net.WebClient
  Add-Proxy-If-Specified($wc)
  Write-Verbose "Downloading $url ..."
  $wc.DownloadFile($url, $tempDownloadFile)

  Do-Unpack $tempDownloadFile $runtimeTempDownload

  md $runtimeFolder -Force | Out-Null
  Console-Write "Installing to $runtimeFolder"
  mv "$runtimeTempDownload\*" $runtimeFolder
  Remove-Item "$runtimeTempDownload" -Force | Out-Null
}

function Do-Unpack {
param(
  [string] $runtimeFile,
  [string] $runtimeFolder
)
  Console-Write "Unpacking to $runtimeFolder"

  $compressionLib = [System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem')

  if($compressionLib -eq $null) {
      try {
          # Shell will not recognize nupkg as a zip and throw, so rename it to zip
          $runtimeZip = [System.IO.Path]::ChangeExtension($runtimeFile, "zip")
          Rename-Item $runtimeFile $runtimeZip
          # Use the shell to uncompress the nupkg
          $shell_app=new-object -com shell.application
          $zip_file = $shell_app.namespace($runtimeZip)
          $destination = $shell_app.namespace($runtimeFolder)
          $destination.Copyhere($zip_file.items(), 0x14) #0x4 = don't show UI, 0x10 = overwrite files
      }
      finally {
        # make it a nupkg again
        Rename-Item $runtimeZip $runtimeFile
      }
  } else {
      [System.IO.Compression.ZipFile]::ExtractToDirectory($runtimeFile, $runtimeFolder)
  }

  If (Test-Path ($runtimeFolder + "\[Content_Types].xml")) {
    Remove-Item ($runtimeFolder + "\[Content_Types].xml")
  }
  If (Test-Path ($runtimeFolder + "\_rels\")) {
    Remove-Item ($runtimeFolder + "\_rels\") -Force -Recurse
  }
  If (Test-Path ($runtimeFolder + "\package\")) {
    Remove-Item ($runtimeFolder + "\package\") -Force -Recurse
  }

  # Clean up the package file itself.
  Remove-Item $runtimeFile -Force
}

function _Install {
param(
  [string] $versionOrAlias,
  [boolean] $isGlobal
)
  if ($versionOrAlias -eq "latest") {
    $versionOrAlias = _Find-Latest (Requested-Platform $defaultRuntime) (Requested-Architecture $defaultArch)
  }

  if ($versionOrAlias.EndsWith(".nupkg")) {
    $runtimeFullName = [System.IO.Path]::GetFileNameWithoutExtension($versionOrAlias)
  } else {
    $runtimeFullName =  Requested-VersionOrAlias $versionOrAlias
  }

  $packageFolder = $userRuntimesPath

  if ($versionOrAlias.EndsWith(".nupkg")) {
    Set-Variable -Name "selectedArch" -Value (Package-Arch $runtimeFullName) -Scope Script
    Set-Variable -Name "selectedRuntime" -Value (Package-Platform $runtimeFullName) -Scope Script

    $runtimeFolder = "$packageFolder\$runtimeFullName"
    $folderExists = Test-Path $runtimeFolder

    if ($folderExists -and $Force) {
      del $runtimeFolder -Recurse -Force
      $folderExists = $false;
    }

    if ($folderExists) {
      Console-Write "Target folder '$runtimeFolder' already exists"
    } else {
      $tempUnpackFolder = Join-Path $packageFolder "temp"
      $tempDownloadFile = Join-Path $tempUnpackFolder "$runtimeFullName.nupkg"

      if(Test-Path $tempUnpackFolder) {
          del "$tempUnpackFolder\*" -recurse
      } else {
          md $tempUnpackFolder -Force | Out-Null
      }
      copy $versionOrAlias $tempDownloadFile

      Do-Unpack $tempDownloadFile $tempUnpackFolder
      md $runtimeFolder -Force | Out-Null
      Console-Write "Installing to $runtimeFolder"
      mv "$tempUnpackFolder\*" $runtimeFolder
      Remove-Item "$tempUnpackFolder" -Force | Out-Null
    }

    $packageVersion = Package-Version $runtimeFullName

    _Use $packageVersion
    if (!$(String-IsEmptyOrWhitespace($Alias))) {
        _Alias-Set $Alias $packageVersion
    }
  }
  else
  {
    Do-Download $runtimeFullName $packageFolder
    _Use $versionOrAlias
    if (!$(String-IsEmptyOrWhitespace($Alias))) {
        _Alias-Set "$Alias" $versionOrAlias
    }
  }

  if ($runtimeFullName.Contains("CoreCLR")) {
    if ($NoNative) {
      Console-Write "Native image generation is skipped"
    }
    else {
      Console-Write "Compiling native images for $runtimeFullName to improve startup performance..."
      Start-Process $CrossGenCommand -Wait
      Console-Write "Finished native image compilation."
    }
  }
}

function _List {
  $runtimeHome = (getenv $HomeEnvVar)
  if (!$runtimeHome) {
    $runtimeHome = "$userHome"
  }

  md ($userHome + "\alias\") -Force | Out-Null
  $aliases = Get-ChildItem ($userHome + "\alias\") | Select @{label='Alias';expression={$_.BaseName}}, @{label='Name';expression={Get-Content $_.FullName }}

  $items = @()
  foreach($portion in $runtimeHome.Split(';')) {
    $path = [System.Environment]::ExpandEnvironmentVariables($portion)
    if (Test-Path("$path\runtimes")) {
      $items += Get-ChildItem ("$path\runtimes\$RuntimePackageName-*") | List-Parts $aliases
    }
  }

  $items | Sort-Object Version, Runtime, Architecture, Alias | Format-Table -AutoSize -Property @{name="Active";expression={$_.Active};alignment="center"}, "Version", "Runtime", "Architecture", "Location", "Alias"
}

filter List-Parts {
  param($aliases)

  $hasBin = Test-Path($_.FullName+"\bin")
  if (!$hasBin) {
    return
  }
  $active = $false
  foreach($portion in $env:Path.Split(';')) {
    # Append \ to the end because otherwise you might see
    # multiple active versions if the folders have the same
    # name prefix (like 1.0-beta and 1.0)
    if ($portion.StartsWith($_.FullName + "\")) {
      $active = $true
    }
  }

  $fullAlias=""
  $delim=""

  foreach($alias in $aliases){
    if($_.Name.Split('\', 2) -contains $alias.Name){
        $fullAlias += $delim + $alias.Alias
        $delim = ", "
    }
  }

  $parts1 = $_.Name.Split('.', 2)
  $parts2 = $parts1[0].Split('-', 4)
  return New-Object PSObject -Property @{
    Active = if ($active) { "*" } else { "" }
    Version = $parts1[1]
    Runtime = $parts2[1]
    OperatingSystem = $parts2[2]
    Architecture = $parts2[3]
    Location = $_.Parent.FullName
    Alias = $fullAlias
  }
}

function _Use {
param(
  [string] $versionOrAlias
)
  Validate-Full-Package-Name-Arguments-Combination $versionOrAlias

  if ($versionOrAlias -eq "none") {
    Console-Write "Removing $RuntimeShortName from process PATH"
    Set-Path (Change-Path $env:Path "" ($userRuntimesPath))

    if ($Persistent) {
      Console-Write "Removing $RuntimeShortName from user PATH"
      $userPath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
      $userPath = Change-Path $userPath "" ($userRuntimesPath)
      [Environment]::SetEnvironmentVariable("Path", $userPath, [System.EnvironmentVariableTarget]::User)
    }
    return;
  }

  $runtimeFullName = Requested-VersionOrAlias $versionOrAlias

  $runtimeBin = Locate-RuntimeBinFromFullName $runtimeFullName
  if ($runtimeBin -eq $null) {
    throw "Cannot find $runtimeFullName, do you need to run '$CommandName install $versionOrAlias'?"
  }

  Console-Write "Adding $runtimeBin to process PATH"
  Set-Path (Change-Path $env:Path $runtimeBin ($userRuntimesPath))

  if ($Persistent) {
    Console-Write "Adding $runtimeBin to user PATH"
    $userPath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
    $userPath = Change-Path $userPath $runtimeBin ($userRuntimesPath)
    [Environment]::SetEnvironmentVariable("Path", $userPath, [System.EnvironmentVariableTarget]::User)
  }
}

function _Alias-List {
  md ($userHome + "\alias\") -Force | Out-Null

  Get-ChildItem ($userHome + "\alias\") | Select @{label='Alias';expression={$_.BaseName}}, @{label='Name';expression={Get-Content $_.FullName }} | Format-Table -AutoSize
}

function _Alias-Get {
param(
  [string] $name
)
  md ($userHome + "\alias\") -Force | Out-Null
  $aliasFilePath=$userHome + "\alias\" + $name + ".txt"
  if (!(Test-Path $aliasFilePath)) {
    Console-Write "Alias '$name' does not exist"
    $script:exitCode = 1 # Return non-zero exit code for scripting
  } else {
    $aliasValue = (Get-Content ($userHome + "\alias\" + $name + ".txt"))
    Console-Write "Alias '$name' is set to $aliasValue"
  }
}

function _Alias-Set {
param(
  [string] $name,
  [string] $value
)
  $runtimeFullName = Requested-VersionOrAlias $value
  $aliasFilePath = $userHome + "\alias\" + $name + ".txt"
  $action = if (Test-Path $aliasFilePath) { "Updating" } else { "Setting" }
  Console-Write "$action alias '$name' to '$runtimeFullName'"
  md ($userHome + "\alias\") -Force | Out-Null
  $runtimeFullName | Out-File ($aliasFilePath) ascii
}

function _Unalias {
param(
  [string] $name
)
  $aliasPath=$userHome + "\alias\" + $name + ".txt"
  if (Test-Path -literalPath "$aliasPath") {
      Console-Write "Removing alias $name"
      Remove-Item -literalPath $aliasPath
  } else {
      Console-Write "Cannot remove alias, '$name' is not a valid alias name"
      $script:exitCode = 1 # Return non-zero exit code for scripting
  }
}

function Locate-RuntimeBinFromFullName() {
param(
  [string] $runtimeFullName
)
  $runtimeHome = (getenv $HomeEnvVar)
  if (!$runtimeHome) {
    $runtimeHome = $userHome
  }
  foreach($portion in $runtimeHome.Split(';')) {
    $path = [System.Environment]::ExpandEnvironmentVariables($portion)
    $runtimeBin = "$path\runtimes\$runtimeFullName\bin"
    if (Test-Path "$runtimeBin") {
      return $runtimeBin
    }
  }
  return $null
}

function Package-Version() {
param(
  [string] $runtimeFullName
)
  return $runtimeFullName -replace '[^.]*.(.*)', '$1'
}

function Package-Platform() {
param(
  [string] $runtimeFullName
)
  return $runtimeFullName -replace "$RuntimePackageName-([^-]*).*", '$1'
}

function Package-Arch() {
param(
  [string] $runtimeFullName
)
  return $runtimeFullName -replace "$RuntimePackageName-[^-]*-[^-]*-([^.]*).*", '$1'
}


function Requested-VersionOrAlias() {
param(
  [string] $versionOrAlias
)
  Validate-Full-Package-Name-Arguments-Combination $versionOrAlias

  $runtimeBin = Locate-RuntimeBinFromFullName $versionOrAlias

  # If the name specified is an existing package, just use it as is
  if ($runtimeBin -ne $null) {
    return $versionOrAlias
  }

  If (Test-Path ($userHome + "\alias\" + $versionOrAlias + ".txt")) {
    $aliasValue = Get-Content ($userHome + "\alias\" + $versionOrAlias + ".txt")
    # Split runtime-coreclr-win-x86.1.0.0-beta3-10922 into version and name sections
    $parts = $aliasValue.Split('.', 2)
    $pkgVersion = $parts[1]
    # runtime-coreclr-win-x86
    $parts = $parts[0].Split('-', 4)
    $pkgPlatform = Requested-Platform $parts[1]
    $pkgArchitecture = Requested-Architecture $parts[3]
  } else {
    $pkgVersion = $versionOrAlias
    $pkgPlatform = Requested-Platform $defaultRuntime
    $pkgArchitecture = Requested-Architecture $defaultArch
  }
  return $RuntimePackageName + "-" + $pkgPlatform + "-win-" + $pkgArchitecture + "." + $pkgVersion
}

function Requested-Platform() {
param(
  [string] $default
)
  if (!(String-IsEmptyOrWhitespace($selectedRuntime))) {return $selectedRuntime}
  return $default
}

function Requested-Architecture() {
param(
  [string] $default
)
  if (!(String-IsEmptyOrWhitespace($selectedArch))) {return $selectedArch}
  return $default
}

function Change-Path() {
param(
  [string] $existingPaths,
  [string] $prependPath,
  [string[]] $removePaths
)
  $newPath = $prependPath
  foreach($portion in $existingPaths.Split(';')) {
    $skip = $portion -eq ""
    foreach($removePath in $removePaths) {
      if ($removePath -and ($portion.StartsWith($removePath))) {
        $skip = $true
      }
    }
    if (!$skip) {
      $newPath = $newPath + ";" + $portion
    }
  }
  return $newPath
}

function Set-Path() {
param(
  [string] $newPath
)
  md $userHome -Force | Out-Null
  $env:Path = $newPath
@"
SET "PATH=$newPath"
"@ | Out-File ($userHome + "\temp-set-envvars.cmd") ascii
}

function Needs-Elevation() {
  if($AssumeElevated) {
    return $false
  }

  $user = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
  $elevated = $user.IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
  return -NOT $elevated
}

function Requested-Switches() {
  $arguments = ""
  if ($X86) {$arguments = "$arguments -x86"}
  if ($X64) {$arguments = "$arguments -x64"}
  if ($selectedRuntime) {$arguments = "$arguments -runtime $selectedRuntime"}
  if ($Persistent) {$arguments = "$arguments -persistent"}
  if ($Force) {$arguments = "$arguments -force"}
  if (!$(String-IsEmptyOrWhitespace($Alias))) {$arguments = "$arguments -alias '$Alias'"}
  return $arguments
}

function Validate-And-Santitize-Switches()
{
  if ($X86 -and $X64) {throw "You cannot select both x86 and x64 architectures"}

  if ($Runtime) {
    $validRuntimes = "CoreCLR", "CLR"
    $match = $validRuntimes | ? { $_ -like $Runtime } | Select -First 1
    if (!$match) {throw "'$runtime' is not a valid runtime"}
    Set-Variable -Name "selectedRuntime" -Value $match.ToLowerInvariant() -Scope Script
  }

  if($Architecture) {
    $validArchitectures = "x64", "x86"
    $match = $validArchitectures | ? { $_ -like $Architecture } | Select -First 1
    if(!$match) {throw "'$architecture' is not a valid architecture"}
    Set-Variable -Name "selectedArch" -Value $match.ToLowerInvariant() -Scope Script
  }
  else {
    if ($X64) {
      Set-Variable -Name "selectedArch" -Value "x64" -Scope Script
    } elseif ($X86) {
      Set-Variable -Name "selectedArch" -Value "x86" -Scope Script
    }
  }

}

$script:capturedOut = @()
function Console-Write() {
param(
  [Parameter(ValueFromPipeline=$true)]
  [string] $message
)
  if($OutputVariable) {
    # Update the capture output
    $script:capturedOut += @($message)
  }

  if(!$Quiet) {
    if ($useHostOutputMethods) {
      try {
        Write-Host $message
      }
      catch {
        $script:useHostOutputMethods = $false
        Console-Write $message
      }
    }
    else {
      [Console]::WriteLine($message)
    }
  }
}

function Console-Write-Error() {
param(
  [Parameter(ValueFromPipeline=$true)]
  [string] $message
)
  if ($useHostOutputMethods) {
    try {
      Write-Error $message
    }
    catch {
      $script:useHostOutputMethods = $false
      Console-Write-Error $message
    }
  }
  else {
   [Console]::Error.WriteLine($message)
  }
}

function Validate-Full-Package-Name-Arguments-Combination() {
param(
  [string] $versionOrAlias
)
  if ($versionOrAlias -like "$RuntimePackageName-*" -and
      ($selectedArch -or $selectedRuntime)) {
    throw "Runtime or architecture cannot be specified when using the full package name."
  }
}

$script:exitCode = 0
try {
  Validate-And-Santitize-Switches
  switch -wildcard ($Command + " " + $Args.Count) {
    "setup 0"           {_Global-Setup}
    "upgrade 0"         {_Upgrade $false}
    "install 1"         {_Install $Args[0] $false}
    "list 0"            {_List}
    "use 1"             {_Use $Args[0]}
    "alias 0"           {_Alias-List}
    "alias 1"           {_Alias-Get $Args[0]}
    "alias 2"           {_Alias-Set $Args[0] $Args[1]}
    "unalias 1"         {_Unalias $Args[0]}
    "help 0"            {_Help}
    " 0"                {_Help}
    default             {throw "Unknown command"};
  }
}
catch {
  Console-Write-Error $_
  Console-Write "Type '$CommandName help' for help on how to use $CommandName."
  $script:exitCode = -1
}
if ($Wait) {
  Console-Write "Press any key to continue ..."
  $x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown,AllowCtrlC")
}

# If the user specified an output variable, push the value up to the parent scope
if($OutputVariable) {
  Set-Variable $OutputVariable $script:capturedOut -Scope 1
}

exit $script:exitCode

# SIG # Begin signature block
# MIIj8AYJKoZIhvcNAQcCoIIj4TCCI90CAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBihdAyG1i1FpIx
# uDUT2fyFw7vomQHm4EVm3wCEM8ANb6CCDZIwggYQMIID+KADAgECAhMzAAAAOI0j
# bRYnoybgAAAAAAA4MA0GCSqGSIb3DQEBCwUAMH4xCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNpZ25p
# bmcgUENBIDIwMTEwHhcNMTQxMDAxMTgxMTE2WhcNMTYwMTAxMTgxMTE2WjCBgzEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjENMAsGA1UECxMETU9Q
# UjEeMBwGA1UEAxMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMIIBIjANBgkqhkiG9w0B
# AQEFAAOCAQ8AMIIBCgKCAQEAwt7Wz+K3fxFl/7NjqfNyufEk61+kHLJEWetvnPtw
# 22VpmquQMV7/3itkEfXtbOkAIYLDkMyCGaPjmWNlir3T1fsgo+AZf7iNPGr+yBKN
# 5dM5701OPoaWTBGxEYSbJ5iIOy3UfRjzBeCtSwQ+Q3UZ5kbEjJ3bidgkh770Rye/
# bY3ceLnDZaFvN+q8caadrI6PjYiRfqg3JdmBJKmI9GNG6rsgyQEv2I4M2dnt4Db7
# ZGhN/EIvkSCpCJooSkeo8P7Zsnr92Og4AbyBRas66Boq3TmDPwfb2OGP/DksNp4B
# n+9od8h4bz74IP+WGhC+8arQYZ6omoS/Pq6vygpZ5Y2LBQIDAQABo4IBfzCCAXsw
# HwYDVR0lBBgwFgYIKwYBBQUHAwMGCisGAQQBgjdMCAEwHQYDVR0OBBYEFMbxyhgS
# CySlRfWC5HUl0C8w12JzMFEGA1UdEQRKMEikRjBEMQ0wCwYDVQQLEwRNT1BSMTMw
# MQYDVQQFEyozMTY0MitjMjJjOTkzNi1iM2M3LTQyNzEtYTRiZC1mZTAzZmE3MmMz
# ZjAwHwYDVR0jBBgwFoAUSG5k5VAF04KqFzc3IrVtqMp1ApUwVAYDVR0fBE0wSzBJ
# oEegRYZDaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jcmwvTWljQ29k
# U2lnUENBMjAxMV8yMDExLTA3LTA4LmNybDBhBggrBgEFBQcBAQRVMFMwUQYIKwYB
# BQUHMAKGRWh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY2VydHMvTWlj
# Q29kU2lnUENBMjAxMV8yMDExLTA3LTA4LmNydDAMBgNVHRMBAf8EAjAAMA0GCSqG
# SIb3DQEBCwUAA4ICAQCecm6ourY1Go2EsDqVN+I0zXvsz1Pk7qvGGDEWM3tPIv6T
# dVZHTXRrmYdcLnSIcKVGb7ScG5hZEk00vtDcdbNdDDPW2AX2NRt+iUjB5YmlLTo3
# J0ce7mjTaFpGoqyF+//Q6OjVYFXnRGtNz73epdy71XqL0+NIx0Z7dZhz+cPI7IgQ
# C/cqLRN4Eo/+a6iYXhxJzjqmNJZi2+7m4wzZG2PH+hhh7LkACKvkzHwSpbamvWVg
# Dh0zWTjfFuEyXH7QexIHgbR+uKld20T/ZkyeQCapTP5OiT+W0WzF2K7LJmbhv2Xj
# 97tj+qhtKSodJ8pOJ8q28Uzq5qdtCrCRLsOEfXKAsfg+DmDZzLsbgJBPixGIXncI
# u+OKq39vCT4rrGfBR+2yqF16PLAF9WCK1UbwVlzypyuwLhEWr+KR0t8orebVlT/4
# uPVr/wLnudvNvP2zQMBxrkadjG7k9gVd7O4AJ4PIRnvmwjrh7xy796E3RuWGq5eu
# dXp27p5LOwbKH6hcrI0VOSHmveHCd5mh9yTx2TgeTAv57v+RbbSKSheIKGPYUGNc
# 56r7VYvEQYM3A0ABcGOfuLD5aEdfonKLCVMOP7uNQqATOUvCQYMvMPhbJvgfuS1O
# eQy77Hpdnzdq2Uitdp0v6b5sNlga1ZL87N/zsV4yFKkTE/Upk/XJOBbXNedrODCC
# B3owggVioAMCAQICCmEOkNIAAAAAAAMwDQYJKoZIhvcNAQELBQAwgYgxCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xMjAwBgNVBAMTKU1pY3Jvc29m
# dCBSb290IENlcnRpZmljYXRlIEF1dGhvcml0eSAyMDExMB4XDTExMDcwODIwNTkw
# OVoXDTI2MDcwODIxMDkwOVowfjELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hp
# bmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jw
# b3JhdGlvbjEoMCYGA1UEAxMfTWljcm9zb2Z0IENvZGUgU2lnbmluZyBQQ0EgMjAx
# MTCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAKvw+nIQHC6t2G6qghBN
# NLrytlghn0IbKmvpWlCquAY4GgRJun/DDB7dN2vGEtgL8DjCmQawyDnVARQxQtOJ
# DXlkh36UYCRsr55JnOloXtLfm1OyCizDr9mpK656Ca/XllnKYBoF6WZ26DJSJhIv
# 56sIUM+zRLdd2MQuA3WraPPLbfM6XKEW9Ea64DhkrG5kNXimoGMPLdNAk/jj3gcN
# 1Vx5pUkp5w2+oBN3vpQ97/vjK1oQH01WKKJ6cuASOrdJXtjt7UORg9l7snuGG9k+
# sYxd6IlPhBryoS9Z5JA7La4zWMW3Pv4y07MDPbGyr5I4ftKdgCz1TlaRITUlwzlu
# ZH9TupwPrRkjhMv0ugOGjfdf8NBSv4yUh7zAIXQlXxgotswnKDglmDlKNs98sZKu
# HCOnqWbsYR9q4ShJnV+I4iVd0yFLPlLEtVc/JAPw0XpbL9Uj43BdD1FGd7P4AOG8
# rAKCX9vAFbO9G9RVS+c5oQ/pI0m8GLhEfEXkwcNyeuBy5yTfv0aZxe/CHFfbg43s
# TUkwp6uO3+xbn6/83bBm4sGXgXvt1u1L50kppxMopqd9Z4DmimJ4X7IvhNdXnFy/
# dygo8e1twyiPLI9AN0/B4YVEicQJTMXUpUMvdJX3bvh4IFgsE11glZo+TzOE2rCI
# F96eTvSWsLxGoGyY0uDWiIwLAgMBAAGjggHtMIIB6TAQBgkrBgEEAYI3FQEEAwIB
# ADAdBgNVHQ4EFgQUSG5k5VAF04KqFzc3IrVtqMp1ApUwGQYJKwYBBAGCNxQCBAwe
# CgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8GA1UdEwEB/wQFMAMBAf8wHwYDVR0j
# BBgwFoAUci06AjGQQ7kUBU7h6qfHMdEjiTQwWgYDVR0fBFMwUTBPoE2gS4ZJaHR0
# cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMvTWljUm9vQ2Vy
# QXV0MjAxMV8yMDExXzAzXzIyLmNybDBeBggrBgEFBQcBAQRSMFAwTgYIKwYBBQUH
# MAKGQmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljUm9vQ2Vy
# QXV0MjAxMV8yMDExXzAzXzIyLmNydDCBnwYDVR0gBIGXMIGUMIGRBgkrBgEEAYI3
# LgMwgYMwPwYIKwYBBQUHAgEWM2h0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lv
# cHMvZG9jcy9wcmltYXJ5Y3BzLmh0bTBABggrBgEFBQcCAjA0HjIgHQBMAGUAZwBh
# AGwAXwBwAG8AbABpAGMAeQBfAHMAdABhAHQAZQBtAGUAbgB0AC4gHTANBgkqhkiG
# 9w0BAQsFAAOCAgEAZ/KGpZjgVHkaLtPYdGcimwuWEeFjkplCln3SeQyQwWVfLiw+
# +MNy0W2D/r4/6ArKO79HqaPzadtjvyI1pZddZYSQfYtGUFXYDJJ80hpLHPM8QotS
# 0LD9a+M+By4pm+Y9G6XUtR13lDni6WTJRD14eiPzE32mkHSDjfTLJgJGKsKKELuk
# qQUMm+1o+mgulaAqPyprWEljHwlpblqYluSD9MCP80Yr3vw70L01724lruWvJ+3Q
# 3fMOr5kol5hNDj0L8giJ1h/DMhji8MUtzluetEk5CsYKwsatruWy2dsViFFFWDgy
# cScaf7H0J/jeLDogaZiyWYlobm+nt3TDQAUGpgEqKD6CPxNNZgvAs0314Y9/HG8V
# fUWnduVAKmWjw11SYobDHWM2l4bf2vP48hahmifhzaWX0O5dY0HjWwechz4GdwbR
# BrF1HxS+YWG18NzGGwS+30HHDiju3mUv7Jf2oVyW2ADWoUa9WfOXpQlLSBCZgB/Q
# ACnFsZulP0V3HjXG0qKin3p6IvpIlR+r+0cjgPWe+L9rt0uX4ut1eBrs6jeZeRhL
# /9azI2h15q/6/IvrC4DqaTuv/DDtBEyO3991bWORPdGdVk5Pv4BXIqF4ETIheu9B
# CrE/+6jMpF3BoYibV3FWTkhFwELJm3ZbCoBIa/15n8G9bW1qyVJzEw16UM0xghW0
# MIIVsAIBATCBlTB+MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSgwJgYDVQQDEx9NaWNyb3NvZnQgQ29kZSBTaWduaW5nIFBDQSAyMDExAhMzAAAA
# OI0jbRYnoybgAAAAAAA4MA0GCWCGSAFlAwQCAQUAoIG6MBkGCSqGSIb3DQEJAzEM
# BgorBgEEAYI3AgEEMBwGCisGAQQBgjcCAQsxDjAMBgorBgEEAYI3AgEVMC8GCSqG
# SIb3DQEJBDEiBCDIT9zu1H3E4RaYxA4J1AK8hudL6BG60mQbZpvCGvuWFDBOBgor
# BgEEAYI3AgEMMUAwPqAkgCIATQBpAGMAcgBvAHMAbwBmAHQAIABBAFMAUAAuAE4A
# RQBUoRaAFGh0dHA6Ly93d3cuYXNwLm5ldC8gMA0GCSqGSIb3DQEBAQUABIIBAK26
# cozT/MDtKSBtvPMRiDO3kAn7qQ0wFPBz/n6cJfGPCiPD+uzLy6v5VSh3PFe3pNCA
# X/UP8BC0+le7NjbWeJ4Gf+S7TOt+l+8/wQ3Wjm1KjixURYU3Ky0vJZnjhoNeb1c+
# CugeK3TEOg7MvG/rcM3eXKHLlxsgAt9OdplaW2utlcdhR6MxvhjLqHtjCaAVTQJQ
# d/OXTvQo09InBvPXKVl/amQeAGST9km2le/GUbXUKaL8gGeClodeSClJ4XBikqiG
# FXqQ1k+vaZvdiVdJ3mQJW0R3KSADD+XxC5MYqpT47Eo32+zFmsbBn/H7diudtn+x
# fR+wCcSzHVW8iz+TswKhghMyMIITLgYKKwYBBAGCNwMDATGCEx4wghMaBgkqhkiG
# 9w0BBwKgghMLMIITBwIBAzEPMA0GCWCGSAFlAwQCAQUAMIIBNQYLKoZIhvcNAQkQ
# AQSgggEkBIIBIDCCARwCAQEGCisGAQQBhFkKAwEwMTANBglghkgBZQMEAgEFAAQg
# Kjn9OaBFy3D417e/vTeKsutBrs0S3qq+Csrm6Bg8sJcCBlTBKPhXURgTMjAxNTAx
# MjkxNzQ1MTcuODIzWjAHAgEBgAIB9KCBsaSBrjCBqzELMAkGA1UEBhMCVVMxCzAJ
# BgNVBAgTAldBMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQg
# Q29ycG9yYXRpb24xDTALBgNVBAsTBE1PUFIxJzAlBgNVBAsTHm5DaXBoZXIgRFNF
# IEVTTjo3RDJFLTM3ODItQjBGNzElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3Rh
# bXAgU2VydmljZaCCDr0wggZxMIIEWaADAgECAgphCYEqAAAAAAACMA0GCSqGSIb3
# DQEBCwUAMIGIMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMTIw
# MAYDVQQDEylNaWNyb3NvZnQgUm9vdCBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkgMjAx
# MDAeFw0xMDA3MDEyMTM2NTVaFw0yNTA3MDEyMTQ2NTVaMHwxCzAJBgNVBAYTAlVT
# MRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQK
# ExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1l
# LVN0YW1wIFBDQSAyMDEwMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA
# qR0NvHcRijog7PwTl/X6f2mUa3RUENWlCgCChfvtfGhLLF/Fw+Vhwna3PmYrW/AV
# UycEMR9BGxqVHc4JE458YTBZsTBED/FgiIRUQwzXTbg4CLNC3ZOs1nMwVyaCo0UN
# 0Or1R4HNvyRgMlhgRvJYR4YyhB50YWeRX4FUsc+TTJLBxKZd0WETbijGGvmGgLvf
# YfxGwScdJGcSchohiq9LZIlQYrFd/XcfPfBXday9ikJNQFHRD5wGPmd/9WbAA5ZE
# fu/QS/1u5ZrKsajyeioKMfDaTgaRtogINeh4HLDpmc085y9Euqf03GS9pAHBIAmT
# eM38vMDJRF1eFpwBBU8iTQIDAQABo4IB5jCCAeIwEAYJKwYBBAGCNxUBBAMCAQAw
# HQYDVR0OBBYEFNVjOlyKMZDzQ3t8RhvFM2hahW1VMBkGCSsGAQQBgjcUAgQMHgoA
# UwB1AGIAQwBBMAsGA1UdDwQEAwIBhjAPBgNVHRMBAf8EBTADAQH/MB8GA1UdIwQY
# MBaAFNX2VsuP6KJcYmjRPZSQW9fOmhjEMFYGA1UdHwRPME0wS6BJoEeGRWh0dHA6
# Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3RzL01pY1Jvb0NlckF1
# dF8yMDEwLTA2LTIzLmNybDBaBggrBgEFBQcBAQROMEwwSgYIKwYBBQUHMAKGPmh0
# dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljUm9vQ2VyQXV0XzIw
# MTAtMDYtMjMuY3J0MIGgBgNVHSABAf8EgZUwgZIwgY8GCSsGAQQBgjcuAzCBgTA9
# BggrBgEFBQcCARYxaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL1BLSS9kb2NzL0NQ
# Uy9kZWZhdWx0Lmh0bTBABggrBgEFBQcCAjA0HjIgHQBMAGUAZwBhAGwAXwBQAG8A
# bABpAGMAeQBfAFMAdABhAHQAZQBtAGUAbgB0AC4gHTANBgkqhkiG9w0BAQsFAAOC
# AgEAB+aIUQ3ixuCYP4FxAz2do6Ehb7Prpsz1Mb7PBeKp/vpXbRkws8LFZslq3/Xn
# 8Hi9x6ieJeP5vO1rVFcIK1GCRBL7uVOMzPRgEop2zEBAQZvcXBf/XPleFzWYJFZL
# dO9CEMivv3/Gf/I3fVo/HPKZeUqRUgCvOA8X9S95gWXZqbVr5MfO9sp6AG9LMEQk
# IjzP7QOllo9ZKby2/QThcJ8ySif9Va8v/rbljjO7Yl+a21dA6fHOmWaQjP9qYn/d
# xUoLkSbiOewZSnFjnXshbcOco6I8+n99lmqQeKZt0uGc+R38ONiU9MalCpaGpL2e
# Gq4EQoO4tYCbIjggtSXlZOz39L9+Y1klD3ouOVd2onGqBooPiRa6YacRy5rYDkea
# gMXQzafQ732D8OE7cQnfXXSYIghh2rBQHm+98eEA3+cxB6STOvdlR3jo+KhIq/fe
# cn5ha293qYHLpwmsObvsxsvYgrRyzR30uIUBHoD7G4kqVDmyW9rIDVWZeodzOwjm
# mC3qjeAzLhIp9cAvVCch98isTtoouLGp25ayp0Kiyc8ZQU3ghvkqmqMRZjDTu3Qy
# S99je/WZii8bxyGvWbWu3EQ8l1Bx16HSxVXjad5XwdHeMMD9zOZN+w2/XU/pnR4Z
# OC+8z1gFLu8NoFA12u8JJxzVs341Hgi62jbb01+P3nSISRIwggTSMIIDuqADAgEC
# AhMzAAAAUf1o4FZkFcM6AAAAAABRMA0GCSqGSIb3DQEBCwUAMHwxCzAJBgNVBAYT
# AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYD
# VQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBU
# aW1lLVN0YW1wIFBDQSAyMDEwMB4XDTE0MDUyMzE3MjAwOVoXDTE1MDgyMzE3MjAw
# OVowgasxCzAJBgNVBAYTAlVTMQswCQYDVQQIEwJXQTEQMA4GA1UEBxMHUmVkbW9u
# ZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMQ0wCwYDVQQLEwRNT1BS
# MScwJQYDVQQLEx5uQ2lwaGVyIERTRSBFU046N0QyRS0zNzgyLUIwRjcxJTAjBgNV
# BAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggEiMA0GCSqGSIb3DQEB
# AQUAA4IBDwAwggEKAoIBAQCdoMHDCiueaTXOKReXVF98kGuwid2UcrElgBt/PMHH
# mOrGTOl+c9s6zVaU+VhAp4QuvSiuv0PxPKhuRYS2C1bHkd2qtEYqFFsxEYrAwyJu
# YLbQf1z+dowW4GpHRxIDnBaCTsmeMYrpgeEpQkeOq9UUFIqywM9wTrQTdj521Uug
# GhaSuXqXxClOTuYWfoyUiVLmVBc3MCfV3JmgJD3j8KrbDf/LWaNpBpmw9dKF4NVY
# QE5cxW74ELcmELrUO4RNYslxwM5jmHYG0w5t04YPjt1vEPdamwS2VM2nh9WMbCZq
# tHExX7DRjilvcSNrrb1CZGMKc4DwQS+LhQVozmG9qyMFAgMBAAGjggEbMIIBFzAd
# BgNVHQ4EFgQURsQO7BFCXdbg4fxRLbhcjVj7mZQwHwYDVR0jBBgwFoAU1WM6XIox
# kPNDe3xGG8UzaFqFbVUwVgYDVR0fBE8wTTBLoEmgR4ZFaHR0cDovL2NybC5taWNy
# b3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMvTWljVGltU3RhUENBXzIwMTAtMDct
# MDEuY3JsMFoGCCsGAQUFBwEBBE4wTDBKBggrBgEFBQcwAoY+aHR0cDovL3d3dy5t
# aWNyb3NvZnQuY29tL3BraS9jZXJ0cy9NaWNUaW1TdGFQQ0FfMjAxMC0wNy0wMS5j
# cnQwDAYDVR0TAQH/BAIwADATBgNVHSUEDDAKBggrBgEFBQcDCDANBgkqhkiG9w0B
# AQsFAAOCAQEAWtViIXrzBsM1Rp+3TjCPFV5I+6mPSuVT7f04J2t7paDp7R3Mc6XK
# cw7r955bYklZn8QprPy3CF9hQ9wFRAMllxnUPgimir3uN5+AT7Q4OrNLCC0UfFJg
# uGVH70NIGUBcBh2cENhuDgV+Clf6Wdt2kNGAX9GbX/SwpQtzGTcPHLLd/qVOqovf
# wvnrH25O/8nhHLn8BndeBR5uF6Lvzfvc15JQ/1qAM0JHvnmAwohkszevEdqJXTED
# YYIc8lm5utllZ/epilMP6qa0kOzT+hhmu5ARgIMv0ivUYuoqxGRb8DneJyq7xiBk
# QoYxrk6fI96Ci7hbBsuiX77RzLAI9M1xiaGCA24wggJWAgEBMIHboYGxpIGuMIGr
# MQswCQYDVQQGEwJVUzELMAkGA1UECBMCV0ExEDAOBgNVBAcTB1JlZG1vbmQxHjAc
# BgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjENMAsGA1UECxMETU9QUjEnMCUG
# A1UECxMebkNpcGhlciBEU0UgRVNOOjdEMkUtMzc4Mi1CMEY3MSUwIwYDVQQDExxN
# aWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNloiUKAQEwCQYFKw4DAhoFAAMVALPt
# +sqglgTwYRiGYELfAzF7wkNfoIHCMIG/pIG8MIG5MQswCQYDVQQGEwJVUzETMBEG
# A1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWlj
# cm9zb2Z0IENvcnBvcmF0aW9uMQ0wCwYDVQQLEwRNT1BSMScwJQYDVQQLEx5uQ2lw
# aGVyIE5UUyBFU046NTdGNi1DMUUwLTU1NEMxKzApBgNVBAMTIk1pY3Jvc29mdCBU
# aW1lIFNvdXJjZSBNYXN0ZXIgQ2xvY2swDQYJKoZIhvcNAQEFBQACBQDYdKXdMCIY
# DzIwMTUwMTI5MTIyODEzWhgPMjAxNTAxMzAxMjI4MTNaMHQwOgYKKwYBBAGEWQoE
# ATEsMCowCgIFANh0pd0CAQAwBwIBAAICC7QwBwIBAAICGZ8wCgIFANh1910CAQAw
# NgYKKwYBBAGEWQoEAjEoMCYwDAYKKwYBBAGEWQoDAaAKMAgCAQACAxbjYKEKMAgC
# AQACAwehIDANBgkqhkiG9w0BAQUFAAOCAQEAIj1Hx+4VTN5oiX7QfwY6aR0GmirY
# 83ADEXlnvuU0acznBDdzuUGhtobspoMNGQuonvHpGeyez09vD3HRdvYiG5+n2gxs
# 1FMcG+4PSwOR1QWcVB6uHFrHflU4Rd3kNe9yOavNlFBHtF+mynpbls7Piej/63wL
# AQTDhn0k3TvZhkN366CN0wBtawdFquZiV7kqgX7uitrsGv12u497R07mgE3vaOhX
# 8Ad3XATDHM/CKlZ5o/I9URbCV3GV/ASQdR9qaksRAwD7MHNPDUQpcycklGFWx/1G
# jf09GBl3SJK+AC9jtze8HjC+Fkt5t4mMKXcOOewTSO2C6akxtboY8yyXADGCAvUw
# ggLxAgEBMIGTMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
# DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24x
# JjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAAAUf1o
# 4FZkFcM6AAAAAABRMA0GCWCGSAFlAwQCAQUAoIIBMjAaBgkqhkiG9w0BCQMxDQYL
# KoZIhvcNAQkQAQQwLwYJKoZIhvcNAQkEMSIEIOYBvNHULHF7HycnwgJuCfVHWqb7
# oW1TTUVsu2qf1+ZLMIHiBgsqhkiG9w0BCRACDDGB0jCBzzCBzDCBsQQUs+36yqCW
# BPBhGIZgQt8DMXvCQ18wgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0Eg
# MjAxMAITMwAAAFH9aOBWZBXDOgAAAAAAUTAWBBQ1DpLbN2pmZH1VhmIR8osFr+Lp
# hzANBgkqhkiG9w0BAQsFAASCAQANXiydd59qcIuvpjnZ4GFkYdFsQDPTf0RcAlbw
# vgqJvEBiaFhjein1ra8o0T/cfJKLvPRRWrd0p70h3g0VD7uRspKfDVCR+3lKO1Of
# 8LziumnA2yd1AdMYaAH8B4mDCAb2WsMrgkhqCuyEvyOJEbW8SRxy4tUBfZfrgOMy
# FVfAjVydb2F/j1Xer5SCtdoFDdyIstlO4f8AzKA3+v9TJfd0lhkMQivZ3yGeCotf
# XZn9bBUqFoNopdUnmu9N4TuI2EFSVAM0NZBADMebsPKdYXBLRWId05Cw5MYLXz6e
# jCfShrbF7EzR/DIqWT6Yu8pBeLu8JlHy5AuNHV9erBoux0z9
# SIG # End signature block
