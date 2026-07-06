# Manual Peer Connection Plan

Status: planning only. No real manual peer connection is implemented yet.

## Current boundary

- `ManualPeerEndpoint` is the only allowed source for user-entered manual targets.
- Raw string targets must not be passed directly into future networking code.
- Raw hostnames are not accepted for manual peer targets.
- DNS lookup is not part of manual peer validation or the planned manual connection path.
- Public IP targets are out of scope for manual peer connections.
- A validated peer remains validation-only until the user explicitly chooses a later connect or send action.
- The UI must keep labelling validated peers as not connected until real connection behavior exists.

## Future connection rules

- Networking code must accept `ManualPeerEndpoint` or an equivalent validated value object, not raw UI input.
- Connect attempts must use a bounded timeout.
- No scanning, retry storms, or background probing.
- No broadcast or multicast in the manual connection path.
- No connection attempt happens from validation alone.
- Receiver confirmation remains required before any transfer is accepted.
- File paths and names must remain sanitized before any received file is written.
- Checksums remain required after transfer.

## Test constraints

- Future connection tests must use loopback or controlled test listeners only.
- Automated tests must not contact real LAN machines.
- Tests and documentation must not include private machine-specific values, device names, MAC addresses, secrets, or personal data.
