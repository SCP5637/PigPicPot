@echo off
setlocal

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
dotnet clean src/main/PigPicPot.csproj -c Release
echo.

echo =====================================
echo   Step 2: Building the project...
echo   (All artifacts will be generated in 'src\main\Build')
echo =====================================
echo.
dotnet publish src/main/PigPicPot.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true /p:EnableCompressionInSingleFile=true

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
set "SOURCE_PUBLISH_DIR=src\main\Build\bin\Release\net8.0-windows\win-x64\publish"

if not exist "%SOURCE_PUBLISH_DIR%" (
    echo.
    echo ERROR: Source publish directory not found! Check the path.
    pause
    exit /b 1
)

echo Copying final application to the 'Release' folder...
xcopy "%SOURCE_PUBLISH_DIR%\*" "Release\" /E /I /Y
echo.

echo =====================================
echo   Step 4: Archiving all build artifacts...
echo =====================================
echo.
echo Moving 'src\main\Build' to the project root for diagnostics...
move "src\main\Build" .
echo.

echo ==============================================================
echo   Process Complete!
echo.
echo   - The clean, distributable application is in the 'Release' folder.
echo   - All raw build artifacts (for debugging) are in the 'Build' folder.
echo   - The 'src\main' source directory is now clean.
echo ==============================================================
echo.
pause