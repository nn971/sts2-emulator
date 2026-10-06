#!/usr/bin/env fish
set -l root (realpath (dirname (status --current-filename))/..)
cd $root

dotnet test Sts2Emulator.sln -c Release
