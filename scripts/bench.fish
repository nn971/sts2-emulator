#!/usr/bin/env fish
set -l root (realpath (dirname (status --current-filename))/..)
cd $root

dotnet run --project benchmarks/Sts2Emulator.Microbench/Sts2Emulator.Microbench.csproj -c Release -- $argv
