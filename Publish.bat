rm -rf Output\Release
rm -rf packages

nuget restore "Xbim.WindowsUI.Nuget.sln"
sleep 2
nuget restore "Xbim.WindowsUI.Nuget.sln"

set buildConfig="%~1"
if "%~1" == "" set buildConfig="Release|Any CPU"

"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" "Xbim.WindowsUI.Nuget.sln" /clean %buildConfig%
"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" "Xbim.WindowsUI.Nuget.sln" /build %buildConfig%

nuget pack Xbim.WindowsUI.package.nuspec
copy Xbim.*.nupkg ..\LocalPackages
del Xbim.*.nupkg

REM ** Skip copy to Install folder if it is not Release build
if %buildConfig% == "Release|Any CPU" GOTO COPYINSTALL
GOTO END

:COPYINSTALL
rm -rf ..\..\EmpireRevit\BAIFC\Installer\BAIfcXplorer\*
cp -rf Output\Release\* ..\..\EmpireRevit\BAIFC\Installer\BAIfcXplorer

:END
