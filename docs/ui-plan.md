# UI Plan & Screenshot Plan

Status: Batch 5G keeps the Avalonia UI shell intentionally **not wired to file
transfer, DNS, LAN discovery, receiver confirmation, or filesystem send
operations**. The manual peer input supports local validation, then an optional
explicit `Probe connection` click. The probe is probe-only: it sends no files
and starts no transfer. Controls that would trigger real file transfer actions
remain disabled.

## Current shell sections

1. **Safety banner** — always-visible status line plus the safety bullet list.
2. **Local device** — placeholder display name and `Not connected · UI shell only` status.
3. **Send files** — drag & drop placeholder zone (visual only, no file reads),
   disabled `Select files…` and `Send (Coming later)` buttons.
4. **Peers** — manual peer input plus `Validate peer` and `Probe connection`
   buttons. `Validate peer` runs local endpoint validation only. A valid
   endpoint is stored as a `ManualPeerEndpoint`; text changes clear that stored
   endpoint and disable probing. `Probe connection` is enabled only after a
   valid endpoint exists, runs only from explicit user click, uses the stored
   validated endpoint rather than raw text, sends no files, starts no transfer,
   performs no receiver confirmation, and does not implement LAN discovery.
5. **Incoming transfer** — placeholder confirmation card with disabled
   `Accept` / `Reject` buttons.
6. **Transfer log** — static sample / empty-state entries.

## Screenshots / GIFs to capture later (once wired)

- Full window at startup showing the safety banner and all sections.
- Send section with real selected files listed (after Batch with file picker).
- Peers section showing a validation-only manual peer with neutral placeholder
  values. Do not show real private IPs, MAC addresses, or hostnames.
- Incoming transfer confirmation card in its active (accept/reject) state.
- Short GIF of a full loopback/LAN transfer with the transfer log updating live.

## Notes

- Keep disabled/"Coming later" labels for file transfer actions until the
  matching feature is real.
- No screenshot should ever expose private IPs, MAC addresses, or hostnames —
  use placeholder / redacted values in published images.
