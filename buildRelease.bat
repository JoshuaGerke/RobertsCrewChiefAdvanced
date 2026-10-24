setlocal
path="c:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin";%path%

cd CrewChiefV4

msbuild CrewChiefV4.csproj /t:Rebuild /p:Configuration=Release

cd ..

cd CrewChiefV4_installer

msbuild CrewChiefV4_installer.wixproj /t:Rebuild /p:Configuration=Release

cd ..
pause
goto :eof

  artifacts:
  - path: Deployment\
    name: Binaries
  - path: CrewChiefV4_installer\Installs\CrewChiefV4-4.14.0.3.msi
  name: Installer
