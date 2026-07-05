# Test Plan

## Guiding rule

Automated tests never touch the real local network. No test may scan, broadcast to, or connect to another physical device. Anything that needs a second real machine is a manual test, done deliberately and separately from the automated suite.

## Levels

### 1. Local unit tests (automated, current)

- `LanFileDrop.Core.Tests` — domain models, transfer manifest, checksum/integrity logic, path/filename sanitization. No file system or network dependencies beyond what the logic under test itself requires.
- `LanFileDrop.Networking.Tests` — protocol encoding/decoding and any logic that can be exercised without a live socket to another machine.

Current state: scaffold smoke tests only (assert the projects reference each other and compile), to be replaced with real coverage starting in Batch 1.

### 2. Local loopback transfer test (automated, later)

Once a transfer prototype exists (Batch 3), exercise a full send/receive cycle entirely on `localhost` within a single test run: one in-process "sender" and one in-process "receiver" talking over a loopback socket. This validates the wire protocol and integrity checking without needing a second physical device.

### 3. Two-PC manual test (manual, later)

Once discovery and the UI shell exist (Batch 5/6), perform a manual test between two real Windows devices on the same LAN:

- Confirm discovery or manual-IP connection succeeds.
- Confirm the receiving side shows the confirmation prompt and no file is written before acceptance.
- Confirm the transferred file's checksum matches the source.
- Confirm rejecting the prompt results in no file being written.

This is a manual, human-run test — it is not part of the automated test suite and is not scripted to run unattended.

## Explicitly out of scope for tests

- No automated LAN scanning or peer discovery in CI or local test runs.
- No test may assume or require a specific second machine (e.g. a machine named MILAPC) to be present.
- No test opens a listener bound to anything other than `localhost`/loopback.
