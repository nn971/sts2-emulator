# ADR 0001: Keep parity-critical mechanics in C#

- Status: Accepted for scaffold
- Date: 2026-10-06

## Context

The project requires behavioral alignment with a C# game and may eventually need high-throughput simulation with a thin research binding.

## Decision

The canonical deterministic emulator core is C#/.NET 9. Python or other languages may provide training/search interfaces but do not own game rules.

## Consequences

Positive:

- closer mapping to audited native semantics;
- easier comparison with community reverse-engineering work;
- strong performance options including NativeAOT;
- one authoritative rule implementation.

Costs:

- research code must cross a language boundary;
- C# performance work needs its own profiling discipline;
- some ML contributors may prefer Python-only code.
