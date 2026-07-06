# Manual Two-PC Smoke Test

This document records the first real two-PC validation of the controlled manual
peer transfer flow. It is a deliberate, human-run smoke test — it is not part of
the automated test suite, is not scripted, and does not make any
production-readiness claim.

## Setup

- Two Windows PCs on the same local network.
- The app run from source on both machines (no installer, no packaging).
- Peer addressing done manually: the receiver's local IP entered by hand on the
  sender, receiver port `5000`.
- No LAN discovery, DNS, broadcast, or multicast involved at any point.
- One small test file used as the transfer payload.

Machine names, full local paths, and private LAN IPs are intentionally not
recorded here, and are masked in the published redacted screenshots under
[docs/media/](media/).

## Steps performed and observed results

1. **Sender: file selection.** The sender selected the test file via
   `Select files...`. The preview showed file name and size only.
2. **Sender: manifest preparation.** `Prepare manifest` was clicked explicitly;
   the app computed the SHA-256 checksum and reported the prepared manifest.
   Nothing was sent at this point.
3. **Receiver: one-shot listener.** On the receiving PC, a receive folder was
   selected explicitly and `Start receiver` was clicked with port `5000`. The
   receiver ran as a single one-shot listener; it did not loop or auto-restart.
4. **Sender: peer validation and send.** The receiver's IP and port were
   entered manually and validated, then `Send prepared transfer` was clicked
   explicitly.
5. **Receiver: incoming request.** The incoming transfer request appeared on
   the receiver with safe metadata (request id, file count, total size, file
   name) — no full paths.
6. **Receiver: explicit Accept required.** No file was written before the
   receiving user clicked `Accept`. Accept/Reject were enabled only while the
   request was pending.
7. **Completion.** After `Accept`, the file was written to the selected receive
   folder and the receive completed successfully. The received file appeared in
   the destination folder as expected.

## Result

The full controlled send/receive flow — manual peer addressing, explicit
manifest preparation, explicit receiver confirmation, checksum-verified write —
was validated in a manual two-PC smoke test on a real local network.

## Explicit boundaries of this smoke test

- Manual and one-off: not automated, not repeated in CI, not scripted.
- One small file, one sender, one receiver, one accept path. Reject and failure
  paths are covered by the automated loopback tests, not by this smoke test.
- Validated the happy path on a trusted home/office LAN; this is not a claim of
  production readiness, robustness under packet loss, or hostile-network safety.
- No LAN discovery was used or exists in the app.
