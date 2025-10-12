@echo off
cls

:question1
echo Do you want to create/overwrite the default config.cfg?
echo.
echo   1. Yes, create default config
echo   2. No, skip
echo.

set /p choice1="Enter your choice (1 or 2) and press Enter: "

if "%choice1%"=="1" (
    echo.
    echo Creating default config.cfg...
    (
        echo # Set to true to show a debug console on startup
        echo debug=false
        echo.
        echo # Set language to zh-CN for Chinese, or en for English
        echo language=zh-CN
        echo.
        echo # Set background image path (relative to the exe^)
        echo background_image=resource/zhu3.jpg
        echo.
        echo # Set to true to lock window resolution
        echo lock_resolution=false
        echo width=1366
        echo height=768
    ) > config.cfg
)

cls
:question2
echo Do you want to write the log of this session to run_log.txt?
echo.
echo   1. Yes, log to file
echo   2. No, display in this window
echo.

set /p choice2="Enter your choice (1 or 2) and press Enter: "

if "%choice2%"=="1" goto run_with_log
if "%choice2%"=="2" goto run_without_log

echo Invalid choice. Defaulting to no log.
goto run_without_log

:run_with_log
echo.
echo Starting PigPicPot... (Output will be written to run_log.txt)
echo.
del run_log.txt 2>nul
dotnet run > run_log.txt 2>&1
goto end_script

:run_without_log
echo.
echo Starting PigPicPot...
echo.
dotnet run
goto end_script

:end_script
echo.
echo Process finished.
pause