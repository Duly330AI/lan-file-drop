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
- Outgoing transfer draft model for preview/review state only; no transfer
  execution or file handles
- Prepared outgoing manifest model with safe file names, optional sizes, and
  `FileChecksum` values; no file handles or local paths

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

- File picker metadata preview, future transfer progress, and the mandatory
  receiver-confirmation dialog
- Wires Core and Networking together
- Contains no business logic of its own — it orchestrates calls into Core and Networking
- Displays manual peer validation state and can invoke the bounded Networking
  probe from a stored validated endpoint; no manual peer transfer flow exists
- Can preview explicitly selected file names and sizes in App state only; no
  file content reading, checksum reading, sending, or receiver-confirmed
  transfer flow exists yet
- Displays App-level send readiness from existing UI state only; no transfer
  orchestration exists yet
- Can create and display an outgoing transfer draft from Core preview metadata;
  this is review-only and does not call Networking or start transfer
- Can retain current selected `IStorageFile` handles in App state only, then
  read them from an explicit manifest preparation action to compute checksums;
  no file handles cross into Core

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

## Selected File Preview Boundary

The App may ask the platform file picker for user-selected files and keep a
preview model containing file name and size only. Full local paths are not part
of the preview state or default display. Selection itself does not read file
contents, compute checksums, scan folders, send files, or start transfer. The
App retains selected `IStorageFile` handles only for the current selection and
disposes them when the selection is replaced, cleared, or the window closes.

## Send Readiness Boundary

The App-level readiness display summarizes existing UI state: manual peer
validation/probe result, selected-file preview count/size, and transfer status.
It is presentation-only and does not orchestrate file sending, checksum reading,
receiver confirmation, LAN discovery, or transfer startup.

## Outgoing Draft Boundary

Core owns the `OutgoingTransferDraft` and `OutgoingTransferDraftFile` preview
models. They contain validated peer display text, safe file names, optional file
sizes, counts, known-size totals, unknown-size state, and a created timestamp.
They do not contain local paths, `IStorageFile` handles, checksums, file
contents, or transfer execution state. The App uses these models only for a
review skeleton; Networking is untouched.

## Prepared Manifest Boundary

The App can explicitly read selected file streams after the user clicks
`Prepare manifest`. It uses Core checksum logic to calculate SHA-256 values and
Core prepared-manifest models to store safe metadata. Core receives safe file
names, optional sizes, and `FileChecksum` values only; it never receives
Avalonia storage handles, full local paths, or file streams. Networking remains
untouched and no transfer starts.

See [manual-peer-connection-plan.md](manual-peer-connection-plan.md) for the
current safety contract for manual peer probing and future connection work.
