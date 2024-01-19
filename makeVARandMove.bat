@echo off
setlocal

rem Set the paths to the file and folder you want to zip
set "filePath=.\meta.json"
set "folderPath=.\Custom"

rem Set the destination zip file path
set "zipFilePath=.\custom.zip"

rem Check if 7z command is available
where 7z >nul 2>nul
if %errorlevel% neq 0 (
    echo "7z command not found. Please install 7-Zip and ensure it's in the system PATH."
    exit /b 1
)

rem Create the zip file
7z a "%zipFilePath%" "%filePath%" "%folderPath%"

if %errorlevel% equ 0 (
    echo "Files successfully zipped to %zipFilePath%"
) else (
    echo "Error occurred while zipping files."
)

endlocal

copy .\custom.zip "D:\Games\Epic Games\Final Fantasy XV\AddonPackages\Gardan\Gardan.Positioner.9.var"
del .\custom.zip