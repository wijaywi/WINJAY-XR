@echo off
rem Sync Winjay.Core & Winjay.Agents sources into the Unity project.
rem Run after changing sources under WinjayCore/ or MockAgent/.
setlocal
cd /d "%~dp0.."

robocopy "WinjayCore\Winjay.Core\Model"     "Unity\WinjayXR\Assets\Winjay.Core\Model"     *.cs /NJH /NJS /NP
robocopy "WinjayCore\Winjay.Core\State"     "Unity\WinjayXR\Assets\Winjay.Core\State"     *.cs /NJH /NJS /NP
robocopy "WinjayCore\Winjay.Core\Events"    "Unity\WinjayXR\Assets\Winjay.Core\Events"    *.cs /NJH /NJS /NP
robocopy "WinjayCore\Winjay.Core\Proposals" "Unity\WinjayXR\Assets\Winjay.Core\Proposals" *.cs /NJH /NJS /NP
robocopy "WinjayCore\Winjay.Core\Mind"      "Unity\WinjayXR\Assets\Winjay.Core\Mind"      *.cs /NJH /NJS /NP
robocopy "MockAgent\Winjay.Agents"          "Unity\WinjayXR\Assets\Winjay.Agents"         *.cs /NJH /NJS /NP
robocopy "MockAgent\Winjay.Agents\Mind"     "Unity\WinjayXR\Assets\Winjay.Agents\Mind"    *.cs /NJH /NJS /NP

echo.
echo Sync complete. Switch back to Unity to recompile.
endlocal
