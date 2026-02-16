@echo off

echo =====================================
echo   Building PigPicPot...
echo =====================================
echo.

dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true /p:EnableCompressionInSingleFile=true

echo.
echo =====================================
echo   Build complete!
echo   You can find the .exe in: bin\Release\net8.0-windows\win-x64\publish
echo =====================================
echo.
pause
