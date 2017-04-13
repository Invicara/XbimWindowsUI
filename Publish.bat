"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe" "Xbim.WindowsUI.Nuget.sln" /build "Release|x64"
nuget pack Xbim.WindowsUI.package.nuspec
copy Xbim.*.nupkg ..\LocalPackages
del Xbim.*.nupkg
