@echo off
echo Compiling ThinkEngine...
dotnet build ThinkEngine.csproj -c Editor
if %ERRORLEVEL% NEQ 0 (
    echo Build failed for Editor configuration. Aborting.
    exit /b %ERRORLEVEL%
)

dotnet build ThinkEngine.csproj -c Standalone
if %ERRORLEVEL% NEQ 0 (
    echo Build failed for Standalone configuration. Aborting.
    exit /b %ERRORLEVEL%
)

echo.
echo Build completed successfully. Copying files...
xcopy /f "./bin/Debug/Editor/System.Runtime.CompilerServices.Unsafe.dll" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/ThinkEngineDLL/" /Y
xcopy /f "./bin/Debug/Editor/System.Numerics.Vectors.dll" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/ThinkEngineDLL/" /Y
xcopy /f "./bin/Debug/Editor/System.Memory.dll" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/ThinkEngineDLL/" /Y
xcopy /f "./bin/Debug/Editor/System.Collections.Immutable.dll" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/ThinkEngineDLL/" /Y
xcopy /f "./bin/Debug/Editor/System.Buffers.dll" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/ThinkEngineDLL/" /Y
xcopy /f "./bin/Debug/Editor/ThinkEngine.dll" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/ThinkEngineDLL/" /Y
xcopy /f "./bin/Debug//Standalone/ThinkEngine.dll" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/" /Y
xcopy /f "./bin/Debug/Editor/Antlr4.Runtime.Standard.dll" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/ThinkEngineDLL/" /Y
xcopy /f "./bin/Debug/dlv2.exe" "./ThinkEngine/Assets/StreamingAssets/ThinkEngineer/ThinkEngine/lib/" /Y
xcopy /f "./bin/Debug/Editor/ThinkEngine.dll.meta" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/ThinkEngineDLL/" /Y
xcopy /f "./bin/Debug/Editor/SensorTemplate.txt" "./ThinkEngine/Assets/Scripts/" /Y
xcopy /f "./bin/Debug/Standalone/ThinkEngine.dll.meta" "./ThinkEngine/Assets/ThinkEngineer/ThinkEngine/Plugins/" /Y

echo Creating Temporal subdirectories...
if not exist ".\ThinkEngine\Assets\ThinkEngineer\ThinkEngine\Temporal\Config" mkdir ".\ThinkEngine\Assets\ThinkEngineer\ThinkEngine\Temporal\Config"
if not exist ".\ThinkEngine\Assets\ThinkEngineer\ThinkEngine\Temporal\Automata" mkdir ".\ThinkEngine\Assets\ThinkEngineer\ThinkEngine\Temporal\Automata"
if not exist ".\ThinkEngine\Assets\ThinkEngineer\ThinkEngine\Temporal\ASP" mkdir ".\ThinkEngine\Assets\ThinkEngineer\ThinkEngine\Temporal\ASP"

7z a  ThinkEnginePlugin.zip .\ThinkEngine\* 