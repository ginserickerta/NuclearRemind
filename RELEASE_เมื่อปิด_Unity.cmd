@echo off
REM ดับเบิลคลิกไฟล์นี้ได้เลย (หลังปิด Unity) เพื่อ build + อัป itch.io ทั้ง WebGL และ Windows
REM หรือปล่อยให้ตัวเฝ้า (armed by Claude) รันให้อัตโนมัติเมื่อปิด Unity
title NUCLEAR ReMind - Release to itch.io
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0release_itch.ps1" %*
echo.
echo เสร็จแล้ว - กดปุ่มใดก็ได้เพื่อปิดหน้าต่าง
pause >nul
