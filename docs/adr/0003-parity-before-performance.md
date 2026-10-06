# ADR 0003: Parity evidence gates performance optimization

- Status: Accepted for scaffold
- Date: 2026-10-06

## Decision

Optimization changes must preserve canonical parity/regression outputs. Performance targets cannot justify silent approximations in the exact engine.

Approximate learned models may be added later behind a distinct interface and must remain rescorable by the exact engine.
