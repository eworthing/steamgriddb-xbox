# Shared ImageMagick dependency check and invocation helpers, dot-sourced by GenerateAssets.ps1
# and GenerateIcons.ps1 so the two do not drift on how they probe for and call `magick` - as they
# had, before this file existed (`magick -version` vs `--version`, `magick convert` vs
# `magick @args`, and only one of the two checking whether the call actually succeeded).

# Returns `magick --version`'s output, or throws (a real terminating error, not just a non-zero
# exit) when `magick` is not on PATH at all. Callers wrap this in try/catch to detect a missing
# install; it says nothing about whether a later invocation will succeed.
function Get-ImageMagickVersion {
    magick --version
}

# Runs `magick` with the given argument list and reports whether it succeeded. A native
# executable's non-zero exit code is not, by itself, a terminating PowerShell error - not even
# with $ErrorActionPreference = "Stop" - so callers must check the returned Succeeded flag rather
# than wrapping this in try/catch and expecting a failed run to land there.
function Invoke-Magick([string[]]$MagickArgs) {
    $output = & magick @MagickArgs 2>&1

    [pscustomobject]@{
        Succeeded = ($LASTEXITCODE -eq 0)
        Output    = $output
    }
}
