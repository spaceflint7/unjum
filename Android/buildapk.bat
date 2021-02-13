setlocal
# make sure we are called in Play directory as Android\buildapk.bat
for /f %%i in ("..") do set ROOT=%%~fi
set SRCDIR=%ROOT%\Play
if not "%SRCDIR%\Android\" == "%~dp0" goto :EOF
set /p KEYSTORE_PASSWORD=<%SRCDIR%\Android\my.keypass
if "%KEYSTORE_PASSWORD%" == "" goto :EOF

set ANDROID_JAR=%ANDROID_HOME%\platforms\android-28\android.jar
set ANDROID_BUILD=%ANDROID_HOME%\build-tools\30.0.2
set FNA_DLL=%ROOT%\_ThirdParty\FNA\bin\Release\FNA.dll
set BLUEBONNET_EXE=%ROOT%\Bluebonnet\.obj\Bluebonnet.exe
set BLUEBONNET_LIB=%ROOT%\Bluebonnet\.obj\Baselib.jar
set BNA_LIB=%ROOT%\Gamelib\.obj\BNA.jar

set OBJDIR=%SRCDIR%\.obj\build\net5.0
if not exist %OBJDIR%\FSharp.jar %BLUEBONNET_EXE% %OBJDIR%\FSharp.Core.dll %OBJDIR%\FSharp.jar

set APKFILE=%SRCDIR%\.obj\unjum.apk
MSBuild %ROOT%\Gamelib\MakeAPK.project -p:INPUT_DLL=%OBJDIR%\Play.dll -p:EXTRA_JAR_1=%OBJDIR%\FSharp.jar -p:EXTRA_JAR_2=%BNA_LIB% -p:EXTRA_JAR_3=%BLUEBONNET_LIB% -p:CONTENT_DIR=%OBJDIR%\Content -p:ICON_PNG=%SRCDIR%\Content\icon.png -p:ANDROID_MANIFEST=%SRCDIR%\Android\AndroidManifest.xml -p:KEYSTORE_FILE=%SRCDIR%\Android\my.keystore -p:KEYSTORE_PWD="%KEYSTORE_PASSWORD%" -p:APK_OUTPUT=%APKFILE% -p:APK_TEMP_DIR=%SRCDIR%\.obj\intermediate\apk
if errorlevel 1 goto :EOF

"%ANDROID_HOME%\platform-tools\adb.exe" install -r %APKFILE%
"%ANDROID_HOME%\platform-tools\adb.exe" logcat -c
"%ANDROID_HOME%\platform-tools\adb.exe" shell am start -n com.spaceflint.unjum/microsoft.xna.framework.Activity
"%ANDROID_HOME%\platform-tools\adb.exe" logcat GnssLocationProvider:S ErrorProcessor:S ctxmgr:S WorkController:S gralloc_ranchu:S Places:S AudioController:S AudioFlinger:S IAudioFlinger:S AudioPolicyIntefaceImpl:S MicrophoneInputStream:S ErrorReporter:S MicroDetectionWorker:S MicroRecognitionRunner:S DeviceStateChecker:S AudioRecord:S AudioRecord-JNI:S android.media.AudioRecord:S MicroDetector:S ActivityThread:S android.os.Debug:S libprocessgroup:S CountryDetector:S netmgr:S Conscrypt:S PlaceInferenceEngine:S PlaceStateUpdater:S
:EOF
