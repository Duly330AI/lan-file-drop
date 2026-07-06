# Manual Peer Connection Plan

Status: first bounded manual peer connection probe exists in Networking and the
App can invoke it from an explicit `Probe connection` click. It does not send
files, start transfers, perform receiver confirmation, or implement LAN
discovery.

## Current boundary

- `ManualPeerEndpoint` is the only allowed source for user-entered manual targets.
- Raw string targets must not be passed directly into future networking code.
- Raw hostnames are not accepted for manual peer targets.
- DNS lookup is not part of manual peer validation or the planned manual connection path.
- Public IP targets are out of scope for manual peer connections.
- A validated peer remains not connected. A probe is reachability-only; future
  connect/send actions remain later work.
- The UI must keep distinguishing validated or probed peers from connected
  transfer sessions until real transfer behavior exists.

## Implemented probe boundary

- Networking probe code accepts `ManualPeerEndpoint`, not raw UI input.
- The probe performs one bounded `TcpClient` connection attempt.
- The probe closes/disposes the client immediately after the attempt.
- No connection attempt happens from validation alone.
- The App invokes the probe only from an explicit `Probe connection` click.
- The probe button is disabled until a valid `ManualPeerEndpoint` exists.
- Text changes clear the stored validated endpoint and disable probing.
- The App passes the stored validated `ManualPeerEndpoint`, not raw UI text.
- The probe sends no files, starts no transfer, and does not perform receiver
  confirmation.

## Future connection rules

- Future transfer code must accept `ManualPeerEndpoint` or an equivalent validated value object, not raw UI input.
- Connect attempts must use a bounded timeout.
- No scanning, retry storms, or background probing.
- No broadcast or multicast in the manual connection path.
- Receiver confirmation remains required before any transfer is accepted.
- File paths and names must remain sanitized before any received file is written.
- Checksums remain required after transfer.

## Test constraints

- Future connection tests must use loopback or controlled test listeners only.
- Automated tests must not contact real LAN machines.
- Tests and documentation must not include private machine-specific values, device names, MAC addresses, secrets, or personal data.
