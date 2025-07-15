@echo off
echo Building and deploying Dexa application...

REM Read version from version.txt
set /p VERSION=<version.txt
echo Current version: %VERSION%

REM Parse version parts (assuming format X.Y.Z)
for /f "tokens=1,2,3 delims=." %%a in ("%VERSION%") do (
    set MAJOR=%%a
    set MINOR=%%b
    set PATCH=%%c
)

REM Increment patch version
set /a PATCH+=1

REM Create new version string
set NEW_VERSION=%MAJOR%.%MINOR%.%PATCH%

REM Update version.txt with new version
echo %NEW_VERSION% > version.txt

echo Updated version to: %NEW_VERSION%
set VERSION=%NEW_VERSION%

REM Clean previous builds
if exist "publish" rmdir /s /q "publish"
if exist "Releases" rmdir /s /q "Releases"

REM Build the application
echo Building application...
dotnet publish Dexa/Dexa.csproj -c Release --self-contained -r win-x64 -o .\publish
if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

REM Create Releases directory
mkdir Releases

REM Generate Velopack package (using vpk instead of Squirrel)
echo Generating Velopack package...
vpk pack --packId Else.Dexa --packVersion %VERSION% --packDir .\publish --mainExe Else.Dexa.exe
if %errorlevel% neq 0 (
    echo Package generation failed!
    pause
    exit /b 1
)

echo Deployment completed successfully!
echo Files generated in Releases folder:
dir Releases

REM Upload to FTP server
echo Uploading files to FTP server...

REM Create FTP script
echo open ***REMOVED*** > ftp_script.txt
echo ***REMOVED***>> ftp_script.txt
echo ***REMOVED*** ftp_script.txt
echo quote pasv >> ftp_script.txt
echo cd dexa >> ftp_script.txt
echo binary >> ftp_script.txt
echo put Releases\Else.Dexa-win-Setup.exe >> ftp_script.txt
echo put Releases\Else.Dexa-%VERSION%-full.nupkg >> ftp_script.txt
echo put Releases\Else.Dexa-win-Portable.zip >> ftp_script.txt
echo put Releases\RELEASES >> ftp_script.txt
echo put Releases\releases.win.json >> ftp_script.txt
echo put Releases\assets.win.json >> ftp_script.txt
echo bye >> ftp_script.txt

REM Execute FTP upload
ftp -s:ftp_script.txt

REM Clean up FTP script
del ftp_script.txt

if %errorlevel% equ 0 (
    echo Files uploaded successfully to FTP server!
) else (
    echo FTP upload failed!
)
