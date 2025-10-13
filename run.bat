@echo off

:main_loop
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
        echo.

        echo # --- Mini Mode Settings ---
        echo # Background image for the mini-mode window
        echo mini_mode_background=resource/zhu1.png

        echo # Resolution for the mini-mode window
        echo mini_mode_width=640
        echo mini_mode_height=480

        echo # Hotkey to toggle mini-mode. Use a combination of Control, Alt, Shift, Win.
        echo # Example: Control+Alt+B
        echo mini_mode_hotkey=LeftCtrl+LeftAlt+B
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
goto restart_prompt

:run_without_log
echo.
echo Starting PigPicPot...
echo.
dotnet run
goto restart_prompt

:restart_prompt
echo.
echo Process finished.
echo.
echo 1. Restart PigPicPot, 2. Exit
set /p restart_choice="Enter your choice (1 or 2) and press Enter: "

if /I "%restart_choice%"=="1" goto main_loop
if /I "%restart_choice%"=="2" exit /b

echo Invalid choice. Exiting...
exit /b