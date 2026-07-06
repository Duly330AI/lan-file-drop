# Architecture

## Solution Layout

```
src/
  LanFileDrop.Core/          domain models, transfer manifest, checksum/integrity logic
  LanFileDrop.Networking/    discovery and transport, depends on Core
  LanFileDrop.App/           Avalonia desktop UI, depends on Core and Networking
tests/
  LanFileDrop.Core.Tests/
  LanFileDrop.Networking.Tests/
```

## Responsibilities

### LanFileDrop.Core

Pure domain logic with no networking and no UI dependency:

- Transfer request/session models
- File manifest and checksum/integrity logic
- Path and filename sanitization rules
- Manual peer endpoint validation for user-entered targets

Kept dependency-free so it can be unit tested in isolation and reused unchanged if a different UI or transport is added later.

### LanFileDrop.Networking

Everything that touches the network:

- Local device discovery (manual IP first, LAN broadcast later)
- Sending/receiving the transfer protocol over TCP
- Translating wire data into/out of Core's domain models
- Manual peer probe code consumes only validated endpoint values from Core, not raw UI strings

Depends on Core, but Core never depends back on Networking.

### LanFileDrop.App

The Avalonia desktop shell:

- File picker, transfer progress, and the mandatory receiver-confirmation dialog
- Wires Core and Networking together
- Contains no business logic of its own — it orchestrates calls into Core and Networking
- Displays manual peer validation state and can invoke the bounded Networking
  probe from a stored validated endpoint; no manual peer transfer flow exists

### Tests

One test project per library, mirroring the library's namespace and scope:

- `LanFileDrop.Core.Tests` — unit tests for domain logic, no network or file-system dependencies beyond what Core itself uses
- `LanFileDrop.Networking.Tests` — tests for the networking layer, using loopback rather than real LAN peers

## Why this split

Keeping Core free of networking and UI concerns means the transfer/integrity logic — the part that most needs to be correct — can be tested quickly and deterministically. Networking and UI can change independently as long as they respect Core's contracts.

## Manual Peer Boundary

Core validates `ManualPeerEndpoint`, the App displays the validation result, and
Networking can perform a bounded probe from a validated endpoint. The App can
invoke that probe only from an explicit user click, using the stored validated
`ManualPeerEndpoint` rather than raw UI text. The probe sends no files, starts
no transfer, performs no receiver confirmation, and no manual peer transfer flow
is implemented. Future networking implementation must continue to accept a
validated endpoint value or equivalent contract object, not raw text from the
UI.

See [manual-peer-connection-plan.md](manual-peer-connection-plan.md) for the
current safety contract for manual peer probing and future connection work.
