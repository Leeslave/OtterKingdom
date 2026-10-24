$ErrorActionPreference = 'Stop'
$verificationRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$verificationTmp = Join-Path $verificationRoot 'tmp'
[void][IO.Directory]::CreateDirectory($verificationTmp)
function New-VerificationProject($original, $name, $pattern, $reference) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $verificationRoot $original)
    foreach ($node in @($project.SelectNodes('//Compile|//Analyzer|//ProjectReference'))) {
        [void]$node.ParentNode.RemoveChild($node)
    }
    foreach ($hint in $project.SelectNodes('//HintPath')) {
        if (-not [IO.Path]::IsPathRooted($hint.InnerText)) {
            $hint.InnerText = [IO.Path]::GetFullPath((Join-Path $verificationRoot $hint.InnerText))
        }
    }
    $group = $project.CreateElement('ItemGroup')
    $compile = $project.CreateElement('Compile')
    $compile.SetAttribute('Include', (Join-Path $verificationRoot $pattern))
    [void]$group.AppendChild($compile)
    if ($reference) {
        $dep = $project.CreateElement('ProjectReference')
        $dep.SetAttribute('Include', $reference)
        [void]$group.AppendChild($dep)
    }
    [void]$project.Project.AppendChild($group)
    $project.Save((Join-Path $verificationTmp $name))
}
New-VerificationProject 'OtterKingdom.csproj' 'RuntimeCheck.csproj' 'Assets/Scripts/**/*.cs' $null
New-VerificationProject 'Assembly-CSharp-Editor.csproj' 'EditorCheck.csproj' 'Assets/Editor/**/*.cs' 'RuntimeCheck.csproj'
New-VerificationProject 'Tests.csproj' 'TestsCheck.csproj' 'Assets/Tests/**/*.cs' 'RuntimeCheck.csproj'
foreach ($name in @('RuntimeCheck.csproj', 'EditorCheck.csproj', 'TestsCheck.csproj')) {
    & dotnet build (Join-Path $verificationTmp $name) -v:q -p:RunAnalyzers=false -p:WarningLevel=0 --ignore-failed-sources
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
