cd /d %~dp0
rmdir /s /q restic-manager-release
dotnet publish ./ResticManager.Web/ResticManager.Web.csproj -c release -o restic-manager-release
cd ./ResticManager.Web/restic-manager-ui
npm run build && cd ..&& cd .. && docker build . -t asupc/restic-manager
pause