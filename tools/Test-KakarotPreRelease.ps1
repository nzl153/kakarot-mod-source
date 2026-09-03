<#
.SYNOPSIS
    发版前静态自检。不需要启动游戏，不需要编译。

.DESCRIPTION
    补齐 Validate-PublicSource.ps1 与 Test-KakarotBranchWorkspace.ps1 没覆盖的几项：

      1. 版本号三处一致（csproj / AssemblyInfo.cs / Kakarot.json）
         —— AssemblyInfo.cs 是手写的且会覆盖 csproj，改漏了编译不报错。
      2. PCK 是否比 Kakarot/ 下的资源新
         —— 本地化住在 PCK 里，只重编 dll 玩家看到的还是旧文案。
      3. 联机确定性禁用项（System.Random / DateTime / Guid / TickCount）
      4. SavedProperty 必须 private；UpgradeInternal 必须配对 FinalizeUpgradeInternal
      5. 三语占位符一致性（{Var} 与 :diff()）
      6. 超大文件报告（仅提示，不算失败）

.PARAMETER PckPath
    要校验新鲜度的 PCK。默认取仓库同级的 _build\Kakarot.pck。

.PARAMETER SkipPck
    跳过 PCK 新鲜度检查（只改了 C# 时用）。

.EXAMPLE
    pwsh -File tools/Test-KakarotPreRelease.ps1
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$PckPath = '',
    [switch]$SkipPck,
    [int]$LargeFileThreshold = 800
)

$ErrorActionPreference = 'Stop'

$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Add-Failure { param([string]$Message) $errors.Add($Message) }
function Add-Warning { param([string]$Message) $warnings.Add($Message) }

function Write-Section { param([string]$Name) Write-Host "`n== $Name ==" -ForegroundColor Cyan }

$codeRoot = Join-Path $RepoRoot 'KakarotCode'
$localeRoot = Join-Path $RepoRoot 'Kakarot/localization'

# ---------------------------------------------------------------- 1. 版本号
Write-Section '1. 版本号三处一致'

$csprojPath = Join-Path $RepoRoot 'KakarotMod.csproj'
$assemblyInfoPath = Join-Path $RepoRoot 'Properties/AssemblyInfo.cs'
$manifestPath = Join-Path $RepoRoot 'Kakarot.json'

$versions = [ordered]@{}

$csproj = Get-Content -LiteralPath $csprojPath -Raw
foreach ($tag in 'Version', 'AssemblyVersion', 'FileVersion') {
    if ($csproj -match "<$tag>([^<]+)</$tag>") {
        $versions["csproj/$tag"] = $Matches[1].Trim()
    }
    else {
        Add-Failure "csproj 缺少 <$tag>"
    }
}

$assemblyInfo = Get-Content -LiteralPath $assemblyInfoPath -Raw
foreach ($attr in 'AssemblyVersion', 'AssemblyFileVersion', 'AssemblyInformationalVersion') {
    if ($assemblyInfo -match "$attr\s*\(\s*""([^""]+)""\s*\)") {
        $versions["AssemblyInfo/$attr"] = $Matches[1].Trim()
    }
    else {
        Add-Failure "AssemblyInfo.cs 缺少 $attr"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$versions['Kakarot.json/version'] = [string]$manifest.version

# 版本号可能写成 5.4.2 或 5.4.2.0，比较时统一补齐到四段
function Normalize-Version {
    param([string]$Value)
    $parts = @($Value -split '\.')
    while ($parts.Count -lt 4) { $parts += '0' }
    return ($parts[0..3] -join '.')
}

$normalized = $versions.GetEnumerator() | ForEach-Object {
    [pscustomobject]@{ Source = $_.Key; Raw = $_.Value; Normalized = Normalize-Version $_.Value }
}
$normalized | Format-Table -AutoSize | Out-String | Write-Host

$distinct = @($normalized.Normalized | Sort-Object -Unique)
if ($distinct.Count -ne 1) {
    Add-Failure "版本号不一致：$($distinct -join ' / ')  —— AssemblyInfo.cs 会覆盖 csproj，改漏了编译不会报错"
}
else {
    Write-Host "OK  三处一致：$($distinct[0])" -ForegroundColor Green
}

# ---------------------------------------------------------------- 2. PCK 新鲜度
Write-Section '2. PCK 新鲜度'

if (-not $PckPath) {
    # 仓库同级的 _build\Kakarot.pck，不写死盘符（公开仓库禁止出现本机绝对路径）
    $PckPath = Join-Path $RepoRoot '../_build/Kakarot.pck'
}

# 公开源码仓库不含美术资源，Kakarot/ 下只有 localization/ 与 MainFile.cs。
# 这时拿本地化文件的时间去比 PCK 毫无意义，会给出一个假的「通过」，必须跳过。
$assetRoot = Join-Path $RepoRoot 'Kakarot'
$hasAssetTree = @(Get-ChildItem -LiteralPath $assetRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne 'localization' }).Count -gt 0

if (-not $hasAssetTree) {
    Write-Host '已跳过：这是不含美术资源的源码仓库，PCK 新鲜度无从判断' -ForegroundColor DarkGray
}
elseif ($SkipPck) {
    Write-Host '已跳过（-SkipPck）' -ForegroundColor DarkGray
}
elseif (-not (Test-Path -LiteralPath $PckPath)) {
    Add-Warning "找不到 PCK：$PckPath（只改了 C# 就用 -SkipPck）"
}
else {
    $pck = Get-Item -LiteralPath $PckPath
    $newest = Get-ChildItem -LiteralPath $assetRoot -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\\.godot\\' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    Write-Host ("PCK      : {0:yyyy-MM-dd HH:mm}  {1:N0} 字节" -f $pck.LastWriteTime, $pck.Length)
    Write-Host ("最新资源 : {0:yyyy-MM-dd HH:mm}  {1}" -f $newest.LastWriteTime, (Resolve-Path -Relative $newest.FullName))

    if ($newest.LastWriteTime -gt $pck.LastWriteTime) {
        Add-Failure "PCK 比资源旧，需要重新导出 —— 本地化住在 PCK 里，只重编 dll 玩家看到的还是旧文案"
    }
    else {
        Write-Host 'OK  PCK 是当前的' -ForegroundColor Green
    }
}

# ---------------------------------------------------------------- 3. 联机确定性
Write-Section '3. 联机确定性禁用项'

$csFiles = Get-ChildItem -LiteralPath $codeRoot -Recurse -Filter *.cs -File

# 逐行剥掉行注释再匹配，避免注释里提到就误报
function Get-CodeLines {
    param([System.IO.FileInfo]$File)
    $n = 0
    foreach ($line in [System.IO.File]::ReadAllLines($File.FullName)) {
        $n++
        $stripped = $line -replace '//.*$', ''
        if ($stripped.Trim()) {
            [pscustomobject]@{ Line = $n; Text = $stripped }
        }
    }
}

$forbidden = [ordered]@{
    'System.Random / new Random()' = 'new\s+Random\s*\(|System\.Random'
    'DateTime'                     = '\bDateTime\s*\.'
    'Guid.NewGuid'                 = '\bGuid\s*\.\s*New'
    'Environment.TickCount'        = '\bEnvironment\s*\.\s*Tick'
}

foreach ($name in $forbidden.Keys) {
    $pattern = $forbidden[$name]
    $hits = foreach ($file in $csFiles) {
        Get-CodeLines $file | Where-Object { $_.Text -match $pattern } | ForEach-Object {
            "{0}:{1}" -f (Resolve-Path -Relative $file.FullName), $_.Line
        }
    }
    if ($hits) {
        Add-Failure "联机不确定性 —— $name`n    $($hits -join "`n    ")"
    }
    else {
        Write-Host "OK  $name 零命中" -ForegroundColor Green
    }
}

# ---------------------------------------------------------------- 4. 状态写入纪律
Write-Section '4. 状态写入纪律'

# 4a. SavedProperty 必须挂在 private 成员上
$savedPropIssues = foreach ($file in $csFiles) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '\[SavedProperty') {
            # 属性声明在紧随其后的第一行非空、非特性行
            for ($j = $i + 1; $j -lt [Math]::Min($i + 5, $lines.Count); $j++) {
                $decl = $lines[$j].Trim()
                if (-not $decl -or $decl.StartsWith('[')) { continue }
                if ($decl -notmatch '^\s*private\b') {
                    "{0}:{1}  {2}" -f (Resolve-Path -Relative $file.FullName), ($j + 1), $decl
                }
                break
            }
        }
    }
}
if ($savedPropIssues) {
    Add-Failure "[SavedProperty] 挂在非 private 成员上（外部可绕过 AssertMutable 直接改存档状态）`n    $($savedPropIssues -join "`n    ")"
}
else {
    Write-Host 'OK  所有 [SavedProperty] 均为 private' -ForegroundColor Green
}

# 4b. UpgradeInternal 必须紧跟 FinalizeUpgradeInternal
$unpaired = foreach ($file in $csFiles) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '\.UpgradeInternal\s*\(') {
            $next = if ($i + 1 -lt $lines.Count) { $lines[$i + 1] } else { '' }
            if ($next -notmatch '\.FinalizeUpgradeInternal\s*\(') {
                "{0}:{1}  {2}" -f (Resolve-Path -Relative $file.FullName), ($i + 1), $lines[$i].Trim()
            }
        }
    }
}
if ($unpaired) {
    Add-Failure "UpgradeInternal 后没有 FinalizeUpgradeInternal（跳过 ModifyCard，两端构造出的卡会不一致）`n    $($unpaired -join "`n    ")"
}
else {
    Write-Host 'OK  UpgradeInternal 全部成对' -ForegroundColor Green
}

# ---------------------------------------------------------------- 5. 三语占位符
Write-Section '5. 三语占位符一致性'

$languages = @('eng', 'zhs', 'jpn')
$reference = 'eng'
$placeholderPattern = '\{[A-Za-z_][A-Za-z0-9_]*(?::[^}]*)?\}'

$refFiles = Get-ChildItem -LiteralPath (Join-Path $localeRoot $reference) -Filter *.json -File
$placeholderIssues = [System.Collections.Generic.List[string]]::new()

foreach ($refFile in $refFiles) {
    $refJson = Get-Content -LiteralPath $refFile.FullName -Raw | ConvertFrom-Json
    foreach ($lang in $languages | Where-Object { $_ -ne $reference }) {
        $otherPath = Join-Path $localeRoot "$lang/$($refFile.Name)"
        if (-not (Test-Path -LiteralPath $otherPath)) { continue }
        $otherJson = Get-Content -LiteralPath $otherPath -Raw | ConvertFrom-Json

        foreach ($prop in $refJson.PSObject.Properties) {
            $key = $prop.Name
            $refValue = [string]$prop.Value
            $otherProp = $otherJson.PSObject.Properties[$key]
            if (-not $otherProp) { continue }   # key 缺失由 Validate-PublicSource 负责

            # {IfUpgraded:show:A|B} 的载荷本身就是待翻译文本，只比较它出现与否
            $refSet = @([regex]::Matches(($refValue -replace '\{IfUpgraded:[^}]*\}', '{IfUpgraded}'), $placeholderPattern) | ForEach-Object { $_.Value }) | Sort-Object -Unique
            $otherSet = @([regex]::Matches(([string]$otherProp.Value -replace '\{IfUpgraded:[^}]*\}', '{IfUpgraded}'), $placeholderPattern) | ForEach-Object { $_.Value }) | Sort-Object -Unique

            if (Compare-Object -ReferenceObject @($refSet) -DifferenceObject @($otherSet)) {
                $placeholderIssues.Add(("{0} [{1}] {2}`n        {3,-4}: {4}`n        {5,-4}: {6}" -f `
                    $refFile.Name, $lang, $key, $reference, ($refSet -join ' '), $lang, ($otherSet -join ' ')))
            }
        }
    }
}

if ($placeholderIssues.Count -gt 0) {
    # 中文风味文是故意写得不同的，这里只警告不失败，由人判断
    Add-Warning "占位符与 $reference 不一致 $($placeholderIssues.Count) 处（中文风味文可能是故意的，逐条看）：`n    $($placeholderIssues -join "`n    ")"
}
else {
    Write-Host 'OK  三语占位符完全一致' -ForegroundColor Green
}

# ---------------------------------------------------------------- 6. 大文件
Write-Section "6. 超大文件（> $LargeFileThreshold 行，仅提示）"

$large = $csFiles | ForEach-Object {
    [pscustomobject]@{
        Lines = ([System.IO.File]::ReadAllLines($_.FullName)).Count
        File  = Resolve-Path -Relative $_.FullName
    }
} | Where-Object { $_.Lines -gt $LargeFileThreshold } | Sort-Object Lines -Descending

if ($large) {
    $large | Format-Table -AutoSize | Out-String | Write-Host
}
else {
    Write-Host "OK  没有超过 $LargeFileThreshold 行的文件" -ForegroundColor Green
}

# ---------------------------------------------------------------- 汇总
Write-Section '汇总'

foreach ($w in $warnings) { Write-Host "WARN  $w" -ForegroundColor Yellow }
foreach ($e in $errors) { Write-Host "FAIL  $e" -ForegroundColor Red }

if ($errors.Count -gt 0) {
    Write-Host "`n$($errors.Count) 项失败，$($warnings.Count) 项警告。发版前必须清零失败项。" -ForegroundColor Red
    exit 1
}

Write-Host "`n全部通过（$($warnings.Count) 项警告）。" -ForegroundColor Green
exit 0
