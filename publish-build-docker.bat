@echo off
chcp 65001 >nul
setlocal
cd /d %~dp0

set IMAGE=asupc/restic-manager
set BUILD_DIR=restic-manager-release
set TAR_FILE=%BUILD_DIR%\restic-manager.tar

echo [1/4] 清理构建目录 %BUILD_DIR% ...
if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
if errorlevel 1 goto :fail

echo [2/4] dotnet publish ...
dotnet publish ./ResticManager.Web/ResticManager.Web.csproj -c release -o %BUILD_DIR%
if errorlevel 1 goto :fail

echo [3/4] npm run build ...
pushd ResticManager.Web\restic-manager-ui
call npm run build
if errorlevel 1 (popd & goto :fail)
popd

echo [4/4] docker build + docker save ...
docker build . -t %IMAGE%
if errorlevel 1 goto :fail
docker save -o %TAR_FILE% %IMAGE%
if errorlevel 1 goto :fail

echo.
echo 构建完成，镜像文件: %CD%\%TAR_FILE%
pause
exit /b 0

:fail
echo.
echo 构建失败，请检查上方错误输出!
pause
exit /b 1
