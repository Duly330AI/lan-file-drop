# Test Plan

## Guiding rule

Automated tests never touch the real local network. No test may scan, broadcast to, or connect to another physical device. Anything that needs a second real machine is a manual test, done deliberately and separately from the automated suite.

## Levels

### 1. Local unit tests (automated, current)

- `LanFileDrop.Core.Tests` — domain models, transfer manifest, outgoing draft
  preview models, prepared outgoing manifest metadata, checksum/integrity logic,
  path/filename sanitization. No file system or network dependencies beyond
  what the logic under test itself requires.
- `LanFileDrop.Networking.Tests` — protocol encoding/decoding and loopback
  sender/receiver behavior that can be exercised without a live socket to
  another machine.

Current state: Core has focused unit coverage for domain models, validation,
checksums, transfer metadata, outgoing draft preview rules, and prepared
manifest metadata rules, including rejection of Windows reserved device file
names. Networking has loopback coverage for the original single-payload
prototype and for the Batch 10A manual peer transfer path, including accept,
reject, checksum mismatch (with temp-file cleanup), multi-file failure that
leaves no final files behind, existing destination protection, and no write
before receiver confirmation completes. Unsafe or duplicate file names return a
clean rejection status to the sender rather than dropping the connection.

### 2. Local loopback transfer test (automated, current)

The automated suite exercises full send/receive cycles entirely on loopback
within a single test run: one in-process sender and one in-process receiver
talking over a loopback socket. This validates the wire protocol, receiver
confirmation boundary, overwrite protection, and integrity checking without
needing a second physical device.

### 3. Single-app manual loopback smoke (manual, current)

When manually checking the App, one Windows instance can exercise the controlled
loopback path without discovery:

- Select a receive folder.
- Start receiver on an explicit local port.
- Select files.
- Validate a manual peer such as loopback plus the selected port.
- Prepare manifest.
- Click `Send prepared transfer`.
- Confirm the incoming request appears and no final file is written before
  `Accept`.
- Click `Accept` and confirm completion status without full path display.
- Repeat with `Reject` to confirm no file is written.

This smoke is manual and intentionally not a scripted UI test.

### 4. Two-PC manual test (manual, performed)

A deliberate manual smoke test between two real Windows devices on the same LAN
has been performed and is recorded in [manual-smoke.md](manual-smoke.md):

- Manual-IP connection to the receiver on an explicit port succeeded (no
  discovery exists or was used).
- The receiving side showed the confirmation prompt and no file was written
  before explicit acceptance.
- After acceptance, the file was written to the selected receive folder and the
  receive completed successfully; the Networking path verifies the checksum
  before promoting a received file.

This was a manual, human-run happy-path smoke test — it is not part of the
automated test suite and is not scripted to run unattended. An automated two-PC
test remains explicitly out of scope. Reject and failure paths are covered by
the automated loopback tests.

## Explicitly out of scope for tests

- No automated LAN scanning or peer discovery in CI or local test runs.
- No test may assume or require a specific second machine to be present.
- No test opens a listener bound to anything other than loopback.
