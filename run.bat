@echo off

:main_loop
cls

echo.
echo Starting PigPicPot... (Output will be written to run_log.txt)
echo.

rem Delete the old log file if it exists
del run_log.txt 2>nul

rem Run the program and redirect all output (stdout and stderr) to the log file
rem We add '-- --data-dir-sln' to pass the argument to the application
dotnet run --project src/main/PigPicPot.csproj -- --data-dir-sln > run_log.txt 2>&1

echo.
echo Process finished. Log has been written to run_log.txt.
echo.
echo 1. Restart PigPicPot, 2. Exit
set /p restart_choice="Enter your choice (1 or 2) and press Enter: "

if /I "%restart_choice%"=="1" goto main_loop
if /I "%restart_choice%"=="2" exit /b

echo Invalid choice. Exiting...
exit /b
