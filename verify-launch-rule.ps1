# Why a Steam game does or does not get a tile in the Xbox app's library.
#
# See MISSING-STEAM-TITLES.md. This is the proof for the rule stated there, and
# it is here because that rule cannot be checked any other way - it lives in
# Steam's binary appinfo.vdf, which nothing on the machine exposes as text.
#
# The rule: a game gets a tile if it has at least one launch record where
#
#   type        is default, none, or omitted   (not option1/2/3, vr, othervr,
#                                               openxr, editor, ...)
#   oslist      includes windows, or is omitted
#   executable  is present, is not a link2ea:// URL, and exists on disk
#
# The last clause is what makes beta branches fall out for free: a record from a
# branch the user is not on names a file that was never installed. An earlier
# version of this script special-cased BetaKey instead and had to keep growing
# sentinels for it (public, NONE, the user's own branch, a developer's renamed
# default branch) - checking the file is both simpler and closer to what the
# Xbox app's own binary does, which has an explicit "executable missing" path.
#
# Graded against a 154-entry library: 141 tiles, 13 without, zero disagreements
# in either direction.
#
# Reads only; touches nothing. Run it with no arguments.

param(
    [string]$AppInfo = "${env:ProgramFiles(x86)}\Steam\appcache\appinfo.vdf",
    # Steam library roots to search for each app's appmanifest_<id>.acf. Not derived from
    # libraryfolders.vdf - that is a feature, not a cleanup this script attempts - so a library on
    # another drive or another machine needs to be passed explicitly.
    [string[]]$SteamLibraryRoots = @("C:\Program Files (x86)\Steam\steamapps", "D:\SteamLibrary\steamapps")
)

$b = [System.IO.File]::ReadAllBytes($AppInfo)
$magic = [BitConverter]::ToUInt32($b, 0)
if ($magic -ne 0x07564429) { throw ("unexpected magic 0x{0:X8} - this parser only handles v29" -f $magic) }

# ---- string table -------------------------------------------------------
$stOff = [BitConverter]::ToInt64($b, 8)
$count = [BitConverter]::ToUInt32($b, $stOff)
$strings = New-Object string[] $count
$p = $stOff + 4
for ($i = 0; $i -lt $count; $i++) {
    $start = $p
    while ($b[$p] -ne 0) { $p++ }
    $strings[$i] = [System.Text.Encoding]::UTF8.GetString($b, $start, $p - $start)
    $p++
}

# ---- binary VDF reader --------------------------------------------------
function Read-Vdf([byte[]]$buf, [ref]$idx, [string[]]$tbl) {
    $map = @{}
    while ($true) {
        $t = $buf[$idx.Value]; $idx.Value++
        if ($t -eq 0x08) { return $map }                       # end of map
        $key = $tbl[[BitConverter]::ToUInt32($buf, $idx.Value)]; $idx.Value += 4
        switch ($t) {
            0x00 { $map[$key] = Read-Vdf $buf $idx $tbl }      # nested map
            0x01 {                                              # string
                $s = $idx.Value
                while ($buf[$idx.Value] -ne 0) { $idx.Value++ }
                $map[$key] = [System.Text.Encoding]::UTF8.GetString($buf, $s, $idx.Value - $s)
                $idx.Value++
            }
            0x02 { $map[$key] = [BitConverter]::ToInt32($buf, $idx.Value);  $idx.Value += 4 }
            0x07 { $map[$key] = [BitConverter]::ToUInt64($buf, $idx.Value); $idx.Value += 8 }
            default { throw "unknown vdf type 0x$('{0:X2}' -f $t) at $($idx.Value)" }
        }
    }
}

# ---- walk apps ----------------------------------------------------------
$apps = @{}
$off = 16
while ($true) {
    $appid = [BitConverter]::ToUInt32($b, $off)
    if ($appid -eq 0) { break }
    $size = [BitConverter]::ToUInt32($b, $off + 4)
    $vdfStart = $off + 8 + 60                 # infoState..binaryVdfSha1
    $i = [ref]$vdfStart
    try { $apps[$appid] = Read-Vdf $b $i $strings } catch { $apps[$appid] = $null }
    $off = $off + 8 + $size
}

# ---- apply the rule -----------------------------------------------------
function Get-LaunchVerdict($app, [string]$InstallDir = '') {
    if ($null -eq $app) { return [pscustomobject]@{ Usable = $null; Types = 'unparsed' } }
    $launch = $app['appinfo']['config']['launch']
    if ($null -eq $launch) { return [pscustomobject]@{ Usable = $false; Types = '(no launch section)' } }

    $seen = @(); $usable = $false
    foreach ($k in ($launch.Keys | Sort-Object { [int]$_ })) {
        $rec  = $launch[$k]
        $type = if ($rec.ContainsKey('type')) { $rec['type'] } else { '' }
        $os   = if ($rec['config'] -and $rec['config'].ContainsKey('oslist')) { $rec['config']['oslist'] } else { '' }
        $exe  = if ($rec.ContainsKey('executable')) { $rec['executable'] } else { '' }
        $seen += "$k=$(if($type){$type}else{'<omitted>'})$(if($os){"/$os"}else{''})"

        $typeOk = $type -in @('', 'default', 'none')
        $osOk   = ($os -eq '') -or ($os -split ',' -contains 'windows')

        # The executable has to be there. A record from a branch the user is not
        # on names a file that was never installed, which is the same "executable
        # missing" failure as a stale record - and needs no BetaKey logic to spot.
        $exeOk = $exe -ne '' -and $exe -notmatch '^link2ea://' -and
                 $InstallDir -ne '' -and (Test-Path (Join-Path $InstallDir $exe))

        if ($typeOk -and $osOk -and $exeOk) { $usable = $true }
    }
    [pscustomobject]@{ Usable = $usable; Types = ($seen -join '  ') }
}

# ---- report over the Xbox manifest --------------------------------------
# Microsoft.GamingApp_8wekyb3d8bbwe is XboxAppData.PackageFamilyName (Services\Xbox\XboxAppData.cs)
# spelled out again on this side of the C#/PowerShell boundary - there is no shared constant to
# reach for across it, so the two copies just have to be kept in sync by hand.
$dir = "$env:LOCALAPPDATA\Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalState\ThirdPartyLibraries\Steam"
$gc  = (Get-Content "$dir\steam.manifest" -Raw | ConvertFrom-Json).gameCache

$rows = foreach ($e in @($gc.PSObject.Properties | Where-Object { $_.Name -ne 'version' })) {
    $id = [uint32]($e.Name -replace '^steam:', '')

    # Where this game is actually installed, from Steam's own install manifest
    $acf = $SteamLibraryRoots |
        ForEach-Object { Join-Path $_ "appmanifest_$id.acf" } | Where-Object { Test-Path $_ } | Select-Object -First 1
    $install = ''
    if ($acf -and (Get-Content $acf -Raw) -match '"installdir"\s+"(.+?)"') {
        $install = Join-Path (Split-Path $acf) "common\$($matches[1])"
    }

    $v = Get-LaunchVerdict $apps[$id] $install
    [pscustomobject]@{
        Id       = $id
        HasTile  = Test-Path "$dir\steam_$id.png"
        Usable   = $v.Usable
        Name     = if ($apps[$id]) { $apps[$id]['appinfo']['common']['name'] } else { '?' }
        Launches = $v.Types
    }
}

"parsed apps in appinfo.vdf : $($apps.Count)"
"manifest entries           : $($rows.Count)"
""
"=== rule vs reality ==="
$rows | Group-Object HasTile, Usable | Sort-Object Name | ForEach-Object {
    "  HasTile={0,-6} Usable={1,-6} count={2}" -f ($_.Name -split ', ')[0], ($_.Name -split ', ')[1], $_.Count
}
""
"=== disagreements (rule says one thing, reality another) ==="
$bad = $rows | Where-Object { $_.HasTile -ne $_.Usable }
if ($bad) { $bad | ForEach-Object { "  {0,-9} tile={1,-6} usable={2,-6} {3}" -f $_.Id, $_.HasTile, $_.Usable, $_.Name } }
else { "  none - rule matches reality for all $($rows.Count) entries" }
""
"=== the entries with no tile, and why ==="
$rows | Where-Object { -not $_.HasTile } | Sort-Object Name | ForEach-Object {
    "  {0,-34} {1}" -f $_.Name, $_.Launches
}
