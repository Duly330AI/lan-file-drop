# Architecture

## Solution Layout

```
src/
  LanFileDrop.Core/          domain models, transfer manifest, checksum/integrity logic
  LanFileDrop.Networking/    manual peer transport, depends on Core (LAN discovery unimplemented)
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

- Manual peer TCP probe and transfer path from validated endpoint values
- Sending/receiving the transfer protocol over TCP with explicit receiver confirmation
- Translating wire data into/out of Core's domain models
- Manual peer probe code consumes only validated endpoint values from Core, not raw UI strings
- LAN discovery remains unimplemented

Depends on Core, but Core never depends back on Networking.

### LanFileDrop.App

The Avalonia desktop shell:

- File picker metadata preview, future transfer progress, and the mandatory
  receiver-confirmation dialog
- Wires Core and Networking together
- Contains no business logic of its own — it orchestrates calls into Core and Networking
- Displays manual peer validation state and can invoke the bounded Networking
  probe from a stored validated endpoint
- Can preview explicitly selected file names and sizes in App state only;
  selection itself does not read file contents, compute checksums, send, or
  start a receiver-confirmed transfer
- Displays App-level send readiness from existing UI state and only enables
  send when selected files, validated peer, and prepared manifest still match
- Can create and display an outgoing transfer draft from Core preview metadata;
  this is review-only and does not call Networking or start transfer
- Can retain current selected `IStorageFile` handles in App state only, then
  read them from an explicit manifest preparation action to compute checksums;
  no file handles cross into Core
- Can start one one-shot manual receiver from explicit user click after a
  receive folder and port are selected, and can pass incoming request metadata
  through an explicit Accept/Reject confirmation UI
- Can invoke `ManualPeerTransferSender.SendAsync` only from the explicit
  `Send prepared transfer` click using the stored validated endpoint and
  prepared manifest

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
no transfer, and performs no receiver confirmation.

Networking now also contains a local manual peer transfer path. The sender
requires a validated `ManualPeerEndpoint`, an explicit
`PreparedOutgoingTransferManifest`, and matching outgoing file streams; it
streams each payload from disk in bounded chunks rather than buffering whole
files. The receiver exposes request metadata through an explicit confirmation
callback and does not read payload frames or write files before acceptance.
After acceptance it streams each payload into a private temporary file while
hashing it, verifies size and SHA-256, and only then promotes the temp file to
its final name; multi-file promotion is all-or-nothing with best-effort rollback
and temp cleanup. The App is now wired to this path in a controlled way: it
passes validated endpoint and prepared manifest objects, not raw UI text, starts
the receiver only from an explicit click, and requires receiver Accept before
Networking writes final files. LAN discovery remains unimplemented. This wired
path has been validated end-to-end in a manual two-PC smoke test
(see [manual-smoke.md](manual-smoke.md)).

The App receiver uses `IPAddress.Any` only in the explicit `Start receiver`
path so a manually addressed LAN peer can reach the selected port. There is no
UDP, broadcast, multicast, discovery loop, background listener, or auto-restart.

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
receiver confirmation, LAN discovery, or transfer startup until the explicit
`Send prepared transfer` click is available and all readiness checks pass.

## Outgoing Draft Boundary

Core owns the `OutgoingTransferDraft` and `OutgoingTransferDraftFile` preview
models. They contain validated peer display text, safe file names, optional file
sizes, counts, known-size totals, unknown-size state, and a created timestamp.
They do not contain local paths, `IStorageFile` handles, checksums, file
contents, or transfer execution state. The App uses these models only for a
review skeleton. Networking is not called until the separate prepared-manifest
send action.

## Prepared Manifest Boundary

The App can explicitly read selected file streams after the user clicks
`Prepare manifest`. It uses Core checksum logic to calculate SHA-256 values and
Core prepared-manifest models to store safe metadata. Core receives safe file
names, optional sizes, and `FileChecksum` values only; it never receives
Avalonia storage handles, full local paths, or file streams. Preparing a
manifest does not call Networking and no transfer starts.

## Receive Folder Boundary

The App can ask the platform folder picker for one receive destination after an
explicit user click. It may call `TryGetLocalPath` only for that selected
receive folder because the Networking receiver requires a local directory path
for safe temp-file and final-file writes. The full path remains internal App
state and is not displayed or logged. The App does not enumerate, scan, or send
folders.

See [manual-peer-connection-plan.md](manual-peer-connection-plan.md) for the
current safety contract for manual peer probing and future connection work.
