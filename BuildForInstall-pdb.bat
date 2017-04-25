rm -rf Output\Release
rm -rf packages

nuget restore "Xbim.WindowsUI.Nuget.sln"
sleep 2
nuget restore "Xbim.WindowsUI.Nuget.sln"

set cputype=%~1
if "%~1" == "" set cputype=x64

"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" "Xbim.WindowsUI.Nuget.sln" /build "ReleaseWithPDB|%cputype%"

nuget pack Xbim.WindowsUI.package.nuspec
copy Xbim.*.nupkg ..\LocalPackages
del Xbim.*.nupkg

REM For build with Debug information, we will skip the copy to the Installer folder
REM rm -rf ..\..\EmpireRevit\BAIFC\Installer\BAIfcXplorer\*
REM cp -r Output\Release\* ..\..\EmpireRevit\BAIFC\Installer\BAIfcXplorer

