# Python binding (planned)

This directory will contain a **thin consumer binding** to the canonical C# emulator.

Its responsibilities may include:

- loading/creating emulator states;
- querying legal actions;
- single and batched stepping;
- forking/snapshot handles;
- extracting canonical or player-visible projections;
- efficient transfer of scalar/array state to Python.

It must not contain parity-critical game mechanics.

Likely implementation options include:

- NativeAOT shared library + C ABI + `ctypes`/CFFI;
- generated C ABI with opaque state handles and batch calls;
- an in-process .NET/Python bridge during early prototyping.

Search, training, strategic databases, and model code belong in the parent AI repository, not in this binding package.
