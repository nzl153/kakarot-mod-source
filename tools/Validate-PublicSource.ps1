$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$errors = [System.Collections.Generic.List[string]]::new()
$nonPublicMarkers = @(
    (-join @([char]0x43, [char]0x6C, [char]0x61, [char]0x75, [char]0x64, [char]0x65)),
    (-join @([char]0x47, [char]0x50, [char]0x54)),
    (-join @([char]0x43, [char]0x6F, [char]0x6D, [char]0x70, [char]0x6F, [char]0x73, [char]0x65, [char]0x72)),
    ((-join @([char]0x41, [char]0x49)) + '[ -]?' + (-join @([char]0x68, [char]0x61, [char]0x6E, [char]0x64, [char]0x6F, [char]0x66, [char]0x66))),
    ([string]::Concat([char]0x4EA4, [char]0x63A5)),
    ([string]::Concat([char]0x5BA1, [char]0x8BA1)),
    (-join @([char]0x61, [char]0x75, [char]0x64, [char]0x69, [char]0x74))
)
$unpackText = [string]::Concat([char]0x89E3, [char]0x5305)
$unpublishedText = -join @([char]0x5C1A, [char]0x672A, [char]0x516C, [char]0x5F00)
$candidatePrepText = -join @([char]0x5019, [char]0x9009, [char]0x6574, [char]0x7406)

$allowedTopLevelFiles = @(
    '.editorconfig',
    '.gitattributes',
    '.gitignore',
    'CONTRIBUTING.md',
    'global.json',
    'Kakarot.json',
    'KakarotMod.csproj',
    'KakarotMod.sln',
    'LICENSE',
    'NOTICE.md',
    'packages.lock.json',
    'README.md'
)

$allowedExactFiles = @(
    'Kakarot/MainFile.cs'
)

$allowedDirectoryRoots = @(
    'Kakarot/localization/',
    'KakarotCode/',
    'Properties/',
    'tools/'
)

$allowedExtensions = @(
    '.cs',
    '.csproj',
    '.editorconfig',
    '.gitattributes',
    '.gdignore',
    '.gitignore',
    '.godot',
    '.json',
    '.md',
    '.ps1',
    '.sln'
)

$allowedExtensionlessFiles = @(
    '.editorconfig',
    '.gitattributes',
    '.gdignore',
    '.gitignore',
    'LICENSE'
)
$generatedDirectories = @('.godot', '.vs', 'bin', 'obj')
$forbiddenText = (@(
    $nonPublicMarkers
) + @(
    ('D:' + '\\'),
    ('C:' + '\\' + 'Users'),
    ('steam' + 'apps'),
    ('Mod' + 'Uploader'),
    ('_sig' + 'probe'),
    $unpackText,
    ('de' + 'compil'),
    ('api[_-]?key\s*[:=]'),
    ('access[_-]?token\s*[:=]'),
    ('password\s*[:=]')
)) -join '|'

function Normalize-RepositoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return ($Path -replace '\\', '/').TrimStart('/')
}

function Test-IsGeneratedPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    foreach ($directory in $generatedDirectories) {
        if ($Path -match "(^|/)$([regex]::Escape($directory))(/|$)") {
            return $true
        }
    }

    return $false
}

function Test-IsAllowedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ($allowedTopLevelFiles -contains $Path -or $allowedExactFiles -contains $Path) {
        return $true
    }

    foreach ($root in $allowedDirectoryRoots) {
        if ($Path.StartsWith($root, [StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

function Test-IsAllowedFileType {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fileName = Split-Path -Leaf $Path
    if ($allowedExtensionlessFiles -contains $fileName) {
        return $true
    }

    $extension = [System.IO.Path]::GetExtension($fileName).ToLowerInvariant()
    return $allowedExtensions -contains $extension
}

function Test-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $normalizedPath = Normalize-RepositoryPath $Path
    if (Test-IsGeneratedPath $normalizedPath) {
        $errors.Add("$Context contains generated directory content: $normalizedPath")
        return
    }

    if (-not (Test-IsAllowedPath $normalizedPath)) {
        $errors.Add("$Context contains a path outside the public allowlist: $normalizedPath")
        return
    }

    if (-not (Test-IsAllowedFileType $normalizedPath)) {
        $errors.Add("$Context contains a non-text or unsupported file type: $normalizedPath")
    }
}

$repositoryFiles = @(Get-ChildItem -LiteralPath $repositoryRoot -Recurse -Force -File | Where-Object {
    $_.FullName -notmatch '[\\/]\.git[\\/]'
})

foreach ($fileInfo in $repositoryFiles) {
    $relativeFile = $fileInfo.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')
    Test-RepositoryPath -Path $relativeFile -Context 'Working tree'
}

foreach ($fileInfo in $repositoryFiles) {
    $relativeFile = $fileInfo.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')
    $normalizedFile = Normalize-RepositoryPath $relativeFile
    if (Test-IsGeneratedPath $normalizedFile) {
        continue
    }

    if (-not (Test-IsAllowedPath $normalizedFile) -or -not (Test-IsAllowedFileType $normalizedFile)) {
        continue
    }

    $matches = Select-String -LiteralPath $fileInfo.FullName -Pattern $forbiddenText -AllMatches -Encoding utf8
    foreach ($match in $matches) {
        $errors.Add("Forbidden text in $normalizedFile line $($match.LineNumber): $($match.Matches.Value -join ', ')")
    }
}

$commits = @(git -C $repositoryRoot rev-list --all)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect Git history.'
}

foreach ($commit in $commits) {
    $historicalPaths = @(git -C $repositoryRoot ls-tree -r --name-only $commit)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect Git tree: $commit"
    }

    foreach ($historicalPath in $historicalPaths) {
        Test-RepositoryPath -Path $historicalPath -Context "Git history $commit"
    }

    $historyMatches = @(git -C $repositoryRoot grep -I -n -E $forbiddenText $commit)
    if ($LASTEXITCODE -eq 0) {
        foreach ($historyMatch in $historyMatches) {
            $errors.Add("Forbidden text in Git history: $historyMatch")
        }
    }
    elseif ($LASTEXITCODE -ne 1) {
        throw "Unable to scan Git history: $commit"
    }
}

if ($commits.Count -gt 0) {
    $trackedFiles = @(git -C $repositoryRoot ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to read tracked files with Git.'
    }

    $workingTreeFiles = @($repositoryFiles | ForEach-Object {
        Normalize-RepositoryPath ($_.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/'))
    })

    $fileSetDifferences = @(Compare-Object -ReferenceObject ($trackedFiles | Sort-Object) -DifferenceObject ($workingTreeFiles | Sort-Object))
    foreach ($difference in $fileSetDifferences) {
        $errors.Add("Tracked/working-tree file set differs: $($difference.InputObject) ($($difference.SideIndicator))")
    }
}

$manifestPath = Join-Path $repositoryRoot 'Kakarot.json'
$projectPath = Join-Path $repositoryRoot 'KakarotMod.csproj'
try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw -Encoding utf8
    $projectVersion = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
    if ([string]$manifest.version -ne [string]$projectVersion) {
        $errors.Add("Version mismatch: Kakarot.json=$($manifest.version), KakarotMod.csproj=$projectVersion")
    }
}
catch {
    $errors.Add("Unable to validate manifest/project versions: $($_.Exception.Message)")
}

foreach ($documentName in @('README.md', 'CONTRIBUTING.md', 'NOTICE.md')) {
    $documentPath = Join-Path $repositoryRoot $documentName
    $placeholderPattern = 'TBD|TODO|' + [regex]::Escape($unpublishedText) + '|' + [regex]::Escape($candidatePrepText)
    $placeholderMatches = Select-String -LiteralPath $documentPath -Pattern $placeholderPattern -Encoding utf8
    foreach ($match in $placeholderMatches) {
        $errors.Add("Publication placeholder in $documentName line $($match.LineNumber): $($match.Line.Trim())")
    }
}

$localizationRoot = Join-Path $repositoryRoot 'Kakarot/localization'
$englishRoot = Join-Path $localizationRoot 'eng'
if (-not (Test-Path -LiteralPath $englishRoot)) {
    $errors.Add('Missing English localization directory.')
}
else {
    $englishFiles = @(Get-ChildItem -LiteralPath $englishRoot -Recurse -File -Filter '*.json')
    $englishRelativePaths = @($englishFiles | ForEach-Object {
        $_.FullName.Substring($englishRoot.Length).TrimStart('\', '/')
    } | Sort-Object)

    foreach ($language in @('zhs', 'jpn')) {
        $languageRoot = Join-Path $localizationRoot $language
        if (-not (Test-Path -LiteralPath $languageRoot)) {
            $errors.Add("Missing localization directory: $language")
            continue
        }

        $languageFiles = @(Get-ChildItem -LiteralPath $languageRoot -Recurse -File -Filter '*.json')
        $languageRelativePaths = @($languageFiles | ForEach-Object {
            $_.FullName.Substring($languageRoot.Length).TrimStart('\', '/')
        } | Sort-Object)

        if (Compare-Object -ReferenceObject $englishRelativePaths -DifferenceObject $languageRelativePaths) {
            $errors.Add("Localization file set differs from eng: $language")
        }

        foreach ($relativePath in $englishRelativePaths) {
            $englishFile = Join-Path $englishRoot $relativePath
            $languageFile = Join-Path $languageRoot $relativePath
            if (-not (Test-Path -LiteralPath $languageFile)) {
                continue
            }

            try {
                $englishJson = Get-Content -LiteralPath $englishFile -Raw -Encoding utf8 | ConvertFrom-Json
                $languageJson = Get-Content -LiteralPath $languageFile -Raw -Encoding utf8 | ConvertFrom-Json
                $englishKeys = @($englishJson.PSObject.Properties.Name | Sort-Object)
                $languageKeys = @($languageJson.PSObject.Properties.Name | Sort-Object)

                if (Compare-Object -ReferenceObject $englishKeys -DifferenceObject $languageKeys) {
                    $errors.Add("Localization top-level keys differ: $language/$relativePath")
                }
            }
            catch {
                $errors.Add("Invalid localization JSON: $language/$relativePath ($($_.Exception.Message))")
            }
        }
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output 'Public source validation passed.'
