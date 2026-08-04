# Runs the unit tests.
#
# These are a plain desktop .NET project, not a UWP one, so nothing is packaged or deployed and a full
# run takes about a second. See TESTING.md for why that works and what it does not cover.
param(
    [string]$Configuration = "Debug",
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$project = "$root\SteamGridDB.Xbox.Tests\SteamGridDB.Xbox.Tests.csproj"

$arguments = @("test", $project, "--configuration", $Configuration, "--nologo")

if ($Filter) {
    $arguments += @("--filter", $Filter)
}

& dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    throw "Tests failed."
}
