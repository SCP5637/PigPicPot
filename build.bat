@echo off
setlocal

:: 保存当前目录
set "ORIGINAL_DIR=%~dp0"
:: 切换到脚本所在目录
cd /d "%ORIGINAL_DIR%"

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo Requesting administrative privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo =====================================
echo   Step 1: Cleaning previous outputs...
echo =====================================
if exist "Release" (
    echo Deleting old 'Release' folder...
    rd /s /q "Release"
)
if exist "Build" (
    echo Deleting old 'Build' folder...
    rd /s /q "Build"
)
echo Running dotnet clean...
dotnet clean src\main\PigPicPot.csproj -c Release
echo.

echo =====================================
echo   Step 2: Building the project...
echo   (All artifacts will be generated in 'Build' folder)
echo =====================================
echo.
dotnet publish src\main\PigPicPot.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true /p:EnableCompressionInSingleFile=true --output Build

if %errorlevel% neq 0 (
    echo.
    echo BUILD FAILED! Aborting.
    pause
    exit /b %errorlevel%
)
echo.

echo =====================================
echo   Step 3: Creating the clean 'Release' package...
echo =====================================
echo.
set "SOURCE_PUBLISH_DIR=Build"

if not exist "%SOURCE_PUBLISH_DIR%" (
    echo.
    echo ERROR: Source publish directory not found! Check the path.
    pause
    exit /b 1
)

echo Creating 'Release' folder...
mkdir "Release" 2>nul
echo Copying final application to the 'Release' folder...
xcopy "%SOURCE_PUBLISH_DIR%\*" "Release\" /E /I /Y

echo Copying resource folder to the 'Release' folder...
xcopy "resource" "Release\resource\" /E /I /Y
echo.

echo =====================================
echo   Step 4: Archiving all build artifacts...
echo =====================================
echo.
echo Renaming 'Build' to 'bin' for diagnostics...
move "Build" "bin" 2>nul
echo.

echo ==============================================================
echo   Process Complete!
echo.
echo   - The clean, distributable application is in the 'Release' folder.
echo   - All raw build artifacts (for debugging) are in the 'bin' folder.
echo ==============================================================
echo.
pause