@echo off
cd /d "%~dp0"
if not exist runtime\win-x64 mkdir runtime\win-x64
if not exist C:\gcc_tmp mkdir C:\gcc_tmp
set TMP=C:\gcc_tmp
set TEMP=C:\gcc_tmp
C:\mingw64\bin\gcc.exe -shared -O2 -static-libgcc -o runtime\win-x64\delivery_native.dll delivery_native.c