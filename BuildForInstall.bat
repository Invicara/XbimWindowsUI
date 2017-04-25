rm -rf Output\Release
rm -rf packages

nuget restore "Xbim.WindowsUI.Nuget.sln"
sleep 2
nuget restore "Xbim.WindowsUI.Nuget.sln"

"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" "Xbim.WindowsUI.Nuget.sln" /build "Release|x64"
nuget pack Xbim.WindowsUI.package.nuspec
copy Xbim.*.nupkg ..\LocalPackages
del Xbim.*.nupkg

rm -rf ..\..\EmpireRevit\BAIFC\Installer\BAIfcXplorer\*
cp -rf Output\Release\* ..\..\EmpireRevit\BAIFC\Installer\BAIfcXplorer

