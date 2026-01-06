#Requires -Version 5.1

# --- Configuration ---
$projectRoot = $PSScriptRoot
$buildConfig = "Release"    # Or "Debug"
$framework = "net35"     # Or your target framework
$buildPath = Join-Path $projectRoot "bin\$buildConfig\$framework"
$targetPathIl2cpp = "C:\Program Files (x86)\Steam\steamapps\common\My Winter Car\BepInEx\plugins"
$exePathIl2cpp = "C:\Program Files (x86)\Steam\steamapps\common\My Winter Car\mywintercar.exe"
$csproj = "ConfigurationManager.sln"  # Relative to $projectRoot

# Initialize error buffer
$lastFilteredErrors = @()

# --- Functions ---
function Stop-GameProcess
{
    $proc = Get-Process "mywintercar" -ErrorAction SilentlyContinue
    if ($proc)
    {
        Write-Host "🛑 Killing existing My Winter Car process..." -ForegroundColor Yellow
        try
        {
            $proc | Stop-Process -Force
            Start-Sleep -Milliseconds 500
            Write-Host "   Stopped." -ForegroundColor Green
        }
        catch
        {
            Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

function Copy-Dll
{
    param (
        [string]$destinationPath,
        [string]$buildSourcePath = $buildPath
    )
    if (-not (Test-Path $buildSourcePath))
    {
        Write-Host "`n⚠️ Build path not found: $buildSourcePath" -ForegroundColor Yellow
        return $false
    }

    $dll = Get-ChildItem $buildSourcePath -Filter *.dll |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $dll)
    {
        Write-Host "`n⚠️ No DLL found in build folder." -ForegroundColor Yellow
        return $false
    }

    if (-not (Test-Path $destinationPath))
    {
        Write-Host "`n⚠️ Creating destination: $destinationPath" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
    }

    try
    {
        Copy-Item $dll.FullName -Destination (Join-Path $destinationPath $dll.Name) -Force
        Write-Host "`n✅ Copied $($dll.Name) to Mods." -ForegroundColor Green
        return $true
    }
    catch
    {
        Write-Host "`n❌ Copy failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Write-ColoredBuildOutput
{
    param([string[]]$outputLines)

    $relevant = $outputLines | Where-Object {
        $_ -and
        $_ -notmatch '^(Determining projects to restore|Restored .* in .* sec|MSBuild version|Time Elapsed)' -and
        $_ -notmatch '^\s*\d+\s+(Warning|Error)\(s\)'
    }

    if ($relevant.Count -eq 0) { return }

    Write-Host "`n--- Build Output Snippets ---"
    foreach ($line in $relevant)
    {
        if ($line -match ":\s*warning\s+[A-Z]{2}\d{4}:")
        {
            Write-Host $line -ForegroundColor Yellow
        }
        elseif ($line -match ":\s*error\s+[A-Z]{2}\d{4}:")
        {
            Write-Host $line -ForegroundColor Red
        }
        else
        {
            Write-Host $line
        }
    }
}

function Remove-BuildFolders
{
    Write-Host "`n🧹 Cleaning build folders..."
    foreach ($f in @("bin", "obj"))
    {
        $path = Join-Path $projectRoot $f
        if (Test-Path $path)
        {
            try
            {
                Remove-Item $path -Recurse -Force
                Write-Host "   Removed '$path'"
            }
            catch
            {
                Write-Host "   Could not remove '$path': $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
    }
    Write-Host "🧹 Clean complete."
}

# --- Main Build+Menu Loop ---
while ($true)
{
    Write-Host "`n====== BUILD ======" -ForegroundColor Cyan
    Write-Host "Working dir: $projectRoot"
    Write-Host "Project file: $csproj`n"

    # Clean & Build
    Remove-BuildFolders
    Write-Host "🔨 Building ($buildConfig)…`n"
    $out = dotnet build (Join-Path $projectRoot $csproj) -c $buildConfig --nologo 2>&1
    $ok = $LASTEXITCODE -eq 0

    # Capture warnings/errors
    $filtered = $out | Where-Object { $_ -match ":\s*(warning|error)\s+[A-Z]{2}\d{4}:" } | Select-Object -Unique
    if ($filtered.Count) { $lastFilteredErrors = $filtered }
    elseif (-not $ok -and $lastFilteredErrors.Count -eq 0)
    {
        $lastFilteredErrors = @("Build failed. Check full output.")
    }

    # Report
    if ($ok)
    {
        $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        Write-Host "[$timestamp]" -ForegroundColor Blue
        Write-Host "✅ Build succeeded." -ForegroundColor Green
                            Stop-GameProcess
    }
    else
    {
        $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        Write-Host "`n[$timestamp] ❌ Build failed." -ForegroundColor Red
        Write-ColoredBuildOutput -outputLines $out
    }

    # --- User Menu ---
    while ($true)
    {
        Write-Host "-------------------`n[Enter] Build again" -ForegroundColor DarkMagenta
        Write-Host "[1]     Copy -> Launch IL2CPP"  -ForegroundColor DarkMagenta
        Write-Host "[2]     Copy Only to IL2CPP" -ForegroundColor DarkMagenta
        Write-Host "[3]     Launch IL2CPP (uses last build)" -ForegroundColor DarkMagenta
        Write-Host "[C]     Copy last errors/warnings" -ForegroundColor DarkMagenta
        Write-Host "[Esc]   Exit" -ForegroundColor DarkMagenta

        $key = [System.Console]::ReadKey($true).Key
        switch ($key)
        {
            'Enter'
            {
                Write-Host "`n🔄 Restarting script…" -ForegroundColor Cyan
                & $PSCommandPath    # re-runs this .ps1
                exit                # stops the current instance
            }
            'D1'
            {
                if (-not $ok)
                {
                    Write-Host "`n⚠️ Build failed; cannot copy+launch." -ForegroundColor Yellow
                }
                elseif (Copy-Dll -destinationPath $targetPathIl2cpp)
                {
                                        Stop-GameProcess
                    Start-Process -FilePath $exePathIl2cpp
                    Write-Host "`n🚀 Launched IL2CPP with DLL." -ForegroundColor Cyan
                }
            }
            'D2'
            {
                if (-not $ok)
                {
                    Write-Host "`n⚠️ Build failed; cannot copy." -ForegroundColor Yellow
                }
                elseif (Copy-Dll -destinationPath $targetPathIl2cpp)
                {
                    Write-Host "`n📂 Opened mods folder." -ForegroundColor Cyan
                    explorer.exe $targetPathIl2cpp
                }
                
            }
            'D3'
            {
                if (-not (Test-Path $exePathIl2cpp))
                {
                    Write-Host "`n❌ IL2CPP EXE not found." -ForegroundColor Red
                }
                else
                {
                                        Stop-GameProcess
                    Start-Process -FilePath $exePathIl2cpp
                    Write-Host "`n🚀 Launched IL2CPP (last build)." -ForegroundColor Cyan
                }
           
            }
            'C'
            {
                if ($lastFilteredErrors.Count -gt 0)
                {
                    "Compiler errors:`n$($lastFilteredErrors -join "`n")" | Set-Clipboard
                    Write-Host "`n📋 Errors copied to clipboard." -ForegroundColor Green
                }
                else
                {
                    Write-Host "`n⚠️ No recorded errors." -ForegroundColor Yellow
                }
            }
            'Escape' { exit }
            default { Write-Host "`n❓ Invalid key." -ForegroundColor Yellow }
        } # end inner switch
    } # end inner menu
} # end main loop
