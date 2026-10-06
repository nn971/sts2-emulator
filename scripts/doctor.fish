#!/usr/bin/env fish
set -l root (realpath (dirname (status --current-filename))/..)
cd $root

echo "dotnet:"
dotnet --info

echo
echo "repo status:"
git status --short 2>/dev/null; or true

echo
echo "CLI:"
dotnet run --project src/Sts2Emulator.Cli/Sts2Emulator.Cli.csproj -- doctor
