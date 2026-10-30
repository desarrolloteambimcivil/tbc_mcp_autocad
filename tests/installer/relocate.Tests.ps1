#Requires -Version 5.1
# No Pester dependency. Execute production functions against isolated temporary files.
# Profile env vars (USERPROFILE, APPDATA, LOCALAPPDATA) are redirected to a
# sandbox under the test root for the whole run, so a function that ignores its
# fixture path cannot reach real user data.
[CmdletBinding()]
param([string]$ResultPath, [string]$TestRootParent = [IO.Path]::GetTempPath())
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path $TestRootParent ('dwg-relocate-tests-' + [guid]::NewGuid().ToString('N'))
$sandboxUserProfile = Join-Path $testRoot 'profile\userprofile'
$sandboxAppData = Join-Path $testRoot 'profile\appdata'
$sandboxLocalAppData = Join-Path $testRoot 'profile\localappdata'
New-Item -ItemType Directory -Path $sandboxUserProfile, $sandboxAppData, $sandboxLocalAppData -Force | Out-Null
$savedUserProfile = $env:USERPROFILE
$savedAppData = $env:APPDATA
$savedLocalAppData = $env:LOCALAPPDATA
$env:USERPROFILE = $sandboxUserProfile
$env:APPDATA = $sandboxAppData
$env:LOCALAPPDATA = $sandboxLocalAppData
if ($env:USERPROFILE -ne $sandboxUserProfile -or $env:APPDATA -ne $sandboxAppData -or $env:LOCALAPPDATA -ne $sandboxLocalAppData) {
    throw 'Profile env redirection failed'
}
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
. (Join-Path $repoRoot 'scripts/setup-validation.ps1')
$installer = Join-Path $repoRoot 'scripts\install.ps1'
$uninstaller = Join-Path $repoRoot 'scripts\uninstall-all.ps1'
foreach ($script in @($installer, $uninstaller)) {
    $tokens = $null; $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        $script, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
    # Dot-source only function definitions; the script bodies run installs and
    # prompts. uninstall-all.ps1 loads last so its standalone copies (e.g.
    # Remove-ServerCopy) win - they are identical on purpose.
    foreach ($fn in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
        . ([scriptblock]::Create($fn.Extent.Text))
    }
}
# Step4 must take the fixture root explicitly; without it the function falls
# back to the real profile and the tests would delete user data.
if (-not (Get-Command Invoke-Step4-Discovery).Parameters.ContainsKey('Root')) {
    throw 'Invoke-Step4-Discovery does not accept -Root; refusing to run'
}
$results = New-Object System.Collections.Generic.List[object]
$script:handled = @()
$script:skipped = @()
$script:failed  = @()
function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Test([string]$Name, [scriptblock]$Body) {
    try { & $Body; $results.Add([pscustomobject]@{name=$Name;passed=$true}); Write-Host "PASS $Name" }
    catch { $results.Add([pscustomobject]@{name=$Name;passed=$false;error=$_.Exception.Message}); Write-Host "FAIL $Name : $_" }
}
function Assert-Throws([scriptblock]$Body, [string]$Pattern) {
    $message = $null
    try { & $Body | Out-Null } catch { $message = $_.Exception.Message }
    Assert ($null -ne $message -and $message -match $Pattern) "Expected error matching '$Pattern', got '$message'"
}
function New-ProductRootFixture {
    # %LOCALAPPDATA%\NourRouas\tbc-acad-mcp\: config, settings, ToolBaker data,
    # discovery files (alias and per-PID) and server copies under server\<name>.
    $root = Join-Path $sandboxLocalAppData 'NourRouas\tbc-acad-mcp'
    New-Item -ItemType Directory -Path "$root/baked/x", "$root/server/current", "$root/server/0.5.0", "$root/captures" -Force | Out-Null
    Set-Content -LiteralPath "$root/dwgmcp.config.json" '{"custom":true}'
    Set-Content -LiteralPath "$root/settings.json" '{"toast":true}'
    Set-Content -LiteralPath "$root/baked/x/tool.json" '{}'
    Set-Content -LiteralPath "$root/acad-2024.json" '{}'
    Set-Content -LiteralPath "$root/acad-2024-4242.json" '{}'
    Set-Content -LiteralPath "$root/captures/c.png" 'png'
    Set-Content -LiteralPath "$root/server/current/tbc-acad-mcp.exe" 'old-server-current'
    Set-Content -LiteralPath "$root/server/0.5.0/tbc-acad-mcp.exe" 'old-server-050'
    Set-Content -LiteralPath (Join-Path $sandboxLocalAppData 'NourRouas\portAcad24.txt') "1234`ntoken`n4242"
    return $root
}
function New-UpstreamFixture {
    # An upstream bimwright/dwg-mcp install: a different product the uninstaller must not touch.
    $up = Join-Path $sandboxLocalAppData 'Bimwright\dwg-mcp'
    New-Item -ItemType Directory -Path "$up/server/current" -Force | Out-Null
    Set-Content -LiteralPath "$up/server/current/dwg-mcp.exe" 'upstream-exe'
    Set-Content -LiteralPath "$up/acad-2024.json" '{}'
    Set-Content -LiteralPath (Join-Path $sandboxLocalAppData 'Bimwright\portAcad24.txt') 'upstream-port'
    return $up
}
function Clear-ProductRoots {
    # The sandbox profile is shared for the whole run; tests must not inherit
    # each other's roots.
    foreach ($vendor in 'NourRouas', 'Bimwright') {
        Remove-Item -LiteralPath (Join-Path $sandboxLocalAppData $vendor) -Recurse -Force -ErrorAction SilentlyContinue
    }
}
function Invoke-Step4 {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param([string]$Root, [switch]$KeepLogs, [switch]$Purge)
    Invoke-Step4-Discovery -Root $Root
}
function Invoke-Wire {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param([string]$Path, [string]$Exe)
    Set-McpConfigEntry -Path $Path -RootKey 'mcpServers' -EntryKind 'standard' -Exe $Exe
}
try {
    Test 'Client repoint: versioned server paths repoint; custom launcher kept' {
        Clear-ProductRoots
        $cfg = Join-Path $testRoot 'mcp.json'
        $exe = "$sandboxLocalAppData\NourRouas\tbc-acad-mcp\server\current\tbc-acad-mcp.exe"
        foreach ($old in @('C:\\Users\\Someone\\AppData\\Local\\NourRouas\\tbc-acad-mcp\\server\\0.5.0\\tbc-acad-mcp.exe',
                          'C:\\Users\\Someone\\AppData\\Local\\NourRouas\\tbc-acad-mcp\\server\\0.6.0\\tbc-acad-mcp.exe')) {
            Set-Content -LiteralPath $cfg -Value ('{ "mcpServers": { "tbc-acad-mcp": { "command": "' + $old + '", "args": ["--read-only"] } } }')
            $res = Invoke-Wire -Path $cfg -Exe $exe
            Assert ($res -eq 'repointed') "expected repointed for $old, got: $res"
            $j = Get-Content -LiteralPath $cfg -Raw | ConvertFrom-Json
            Assert ($j.mcpServers.'tbc-acad-mcp'.command -eq $exe) "$old not repointed to exactly $exe, got: $($j.mcpServers.'tbc-acad-mcp'.command)"
            Assert ($j.mcpServers.'tbc-acad-mcp'.args[0] -eq '--read-only') 'existing args lost'
        }
        # The product root's own current\ is never repointed.
        Set-Content -LiteralPath $cfg -Value ('{ "mcpServers": { "tbc-acad-mcp": { "command": "' + $exe.Replace('\', '\\') + '" } } }')
        $res = Invoke-Wire -Path $cfg -Exe $exe
        Assert ($res -eq 'already') "expected already for the current path, got: $res"
        # A custom launcher is reported and left alone.
        Set-Content -LiteralPath $cfg -Value '{ "mcpServers": { "tbc-acad-mcp": { "command": "C:\\tools\\my-wrapper.cmd" } } }'
        $res = Invoke-Wire -Path $cfg -Exe $exe
        Assert ($res -eq 'custom') "expected custom, got: $res"
        Assert ((Get-Content -LiteralPath $cfg -Raw).Contains('my-wrapper.cmd')) 'custom launcher clobbered'
    }
    Test 'Client wiring never edits an upstream dwg-mcp entry' {
        $cfg = Join-Path $testRoot 'mcp-upstream.json'
        $exe = "$sandboxLocalAppData\NourRouas\tbc-acad-mcp\server\current\tbc-acad-mcp.exe"
        $upstream = '{ "mcpServers": { "dwg-mcp": { "command": "C:\\Users\\Someone\\AppData\\Local\\Bimwright\\dwg-mcp\\server\\0.6.0\\dwg-mcp.exe" }, "bimwright-dwg": { "command": "bimwright-dwg" } } }'
        Set-Content -LiteralPath $cfg -Value $upstream
        $res = Invoke-Wire -Path $cfg -Exe $exe
        Assert ($res -eq 'added') "expected added, got: $res"
        $j = Get-Content -LiteralPath $cfg -Raw | ConvertFrom-Json
        Assert ($j.mcpServers.'tbc-acad-mcp'.command -eq $exe) 'tbc-acad-mcp entry not added'
        Assert ($j.mcpServers.'dwg-mcp'.command -like '*Bimwright\dwg-mcp\server\0.6.0\dwg-mcp.exe') 'upstream dwg-mcp entry changed'
        Assert ($j.mcpServers.'bimwright-dwg'.command -eq 'bimwright-dwg') 'upstream bimwright-dwg entry changed'
        $removed = Set-McpConfigEntry -Path $cfg -RootKey 'mcpServers' -EntryKind 'standard' -Remove
        Assert ($removed -eq 'removed') "expected removed, got: $removed"
        $j = Get-Content -LiteralPath $cfg -Raw | ConvertFrom-Json
        Assert (-not $j.mcpServers.PSObject.Properties['tbc-acad-mcp']) 'tbc-acad-mcp entry not removed'
        Assert ($j.mcpServers.PSObject.Properties['dwg-mcp'] -and $j.mcpServers.PSObject.Properties['bimwright-dwg']) 'remove touched upstream entries'
    }
    Test 'Uninstall default removes server copies and discovery, keeps personal data' {
        Clear-ProductRoots
        $root = New-ProductRootFixture
        $script:handled=@(); $script:skipped=@(); $script:failed=@()
        Invoke-Step4 -Root $root
        Assert (-not (Test-Path -LiteralPath "$root/server")) 'server copies not removed'
        Assert (-not (Test-Path -LiteralPath "$root/acad-2024.json")) 'discovery file not removed'
        Assert (-not (Test-Path -LiteralPath "$root/acad-2024-4242.json")) 'per-instance discovery file not removed'
        Assert (-not (Test-Path -LiteralPath "$sandboxLocalAppData/NourRouas/portAcad24.txt")) 'legacy portAcad24.txt not removed'
        Assert (Test-Path -LiteralPath "$root/dwgmcp.config.json") 'config removed'
        Assert (Test-Path -LiteralPath "$root/settings.json") 'settings removed'
        Assert (Test-Path -LiteralPath "$root/baked/x/tool.json") 'baked data removed'
        Assert (Test-Path -LiteralPath "$root/captures/c.png") 'captures removed'
        Assert ($script:handled -contains 'step4-discovery') 'step4 not reported'
        Clear-ProductRoots
    }
    Test 'Uninstall -Purge removes the product root but never the NourRouas parent, siblings or upstream dwg-mcp' {
        Clear-ProductRoots
        $vendor = Join-Path $sandboxLocalAppData 'NourRouas'
        New-Item -ItemType Directory -Path "$vendor/tbc-revit-mcp" -Force | Out-Null
        Set-Content -LiteralPath "$vendor/tbc-revit-mcp/keep.txt" 'k'
        $root = New-ProductRootFixture
        $upstream = New-UpstreamFixture
        $script:handled=@(); $script:skipped=@(); $script:failed=@()
        Invoke-Step4 -Root $root -Purge
        Assert (-not (Test-Path -LiteralPath $root)) 'product root survived -Purge'
        Assert (Test-Path -LiteralPath $vendor) 'NourRouas vendor parent removed'
        Assert ((Get-Content -LiteralPath "$vendor/tbc-revit-mcp/keep.txt" -Raw).Trim() -eq 'k') 'sibling product touched'
        Assert ((Get-Content -LiteralPath "$upstream/server/current/dwg-mcp.exe" -Raw).Trim() -eq 'upstream-exe') 'upstream server touched'
        Assert (Test-Path -LiteralPath "$upstream/acad-2024.json") 'upstream discovery file touched'
        Assert ((Get-Content -LiteralPath "$sandboxLocalAppData/Bimwright/portAcad24.txt" -Raw).Trim() -eq 'upstream-port') 'upstream portAcad24.txt touched'
        Clear-ProductRoots
    }
} finally {
    $env:USERPROFILE = $savedUserProfile
    $env:APPDATA = $savedAppData
    $env:LOCALAPPDATA = $savedLocalAppData
    $report = [ordered]@{powershell=$PSVersionTable.PSVersion.ToString();installerSha256=(Get-FileHash $installer -Algorithm SHA256).Hash;uninstallerSha256=(Get-FileHash $uninstaller -Algorithm SHA256).Hash;results=@($results.ToArray())}
    if ($ResultPath) { $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ResultPath -Encoding UTF8 }
    # Only remove the uniquely created test directory under the resolved temp root.
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $tempRoot = [IO.Path]::GetFullPath($TestRootParent).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
if (@($results | Where-Object { -not $_.passed }).Count) { throw 'Installer and uninstaller regression tests failed' }
