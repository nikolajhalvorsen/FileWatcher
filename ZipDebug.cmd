del .\FileWatcher-debug.zip > NUL
powershell -Command " & {Get-ChildItem -Path ".\src\bin\Debug\net7.0" | Compress-Archive -DestinationPath ".\FileWatcher-debug.zip"}"
pause