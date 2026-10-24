@echo off
:: Packs DynamicIslands\ into DynamicIslands.rmod. The work is done by pack.ps1
:: (the original TeKGameR script only worked when this folder was named "DynamicIslands").
:: Pass -Install to also copy the .rmod into Raft's mods folder.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0pack.ps1" %*
