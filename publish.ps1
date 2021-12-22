param (
    [Parameter(Mandatory=$true)]
    [String] $apiKey
)


function PublishProject {
    param (
        [Parameter(Mandatory=$true)]
        [String] $projectPath
    )

    [xml]$proj = Get-Content $projectPath;

    $versionNode = Select-Xml -Xml $proj -XPath "//Version" | Select-Object @{l="Version";e={$_.node.InnerXML}}
    $packageNameNode = Select-Xml -Xml $proj -XPath "//PackageId" | Select-Object @{l="PackageId";e={$_.node.InnerXML}}

    $version = $versionNode.Version
    $packageId = $packageNameNode.PackageId
    
    Write-Host "Looking for $version $packageId"

    try {
        $response = Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$packageId/index.json"
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "No remote infomration, could be first time package - got $code"

        dotnet pack $projectPath -c Release -o ./packs/$packageId
        dotnet nuget push "./packs/$packageId/$packageId.$version.nupkg" --source https://api.nuget.org/v3/index.json --api-key $apiKey;

        return; 
    }

    $versionList = $response.Content | ConvertFrom-Json
    $serverPackageVersion = $versionList.versions[$versionList.versions.Length - 1]

    Write-Host "$serverPackageVersion $version"

    # if the versions are equal, then no need to push packages
    if ($version -eq $serverPackageVersion) { return; }

    Write-Host "Creating and publishing $packageId.$version"

    dotnet pack $projectPath -c Release -o ./packs/$packageId
    dotnet nuget push "./packs/$packageId/$packageId.$version.nupkg" --source https://api.nuget.org/v3/index.json --api-key $apiKey;
}

Get-ChildItem -Path ./src/**/*.fsproj -Recurse 
    | Select-Object @{l="Name";e={$_.FullName}} 
    | ForEach-Object -Process { PublishProject -projectPath $_.Name }
