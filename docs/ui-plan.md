# UI Plan & Screenshot Plan

Status: Batch 4 delivers a **static Avalonia UI shell**. It presents the product
layout but is intentionally **not wired to any transfer, socket, or filesystem
operation**. Every interactive control that could trigger a real action is
disabled and labelled.

## Current shell sections

1. **Safety banner** — always-visible status line plus the safety bullet list.
2. **Local device** — placeholder display name and `Not connected · UI shell only` status.
3. **Send files** — drag & drop placeholder zone (visual only, no file reads),
   disabled `Select files…` and `Send (Coming later)` buttons.
4. **Peers** — empty peer list placeholder; manual IP input disabled and labelled
   `Coming in Batch 5`.
5. **Incoming transfer** — placeholder confirmation card with disabled
   `Accept` / `Reject` buttons.
6. **Transfer log** — static sample / empty-state entries.

## Screenshots / GIFs to capture later (once wired)

- Full window at startup showing the safety banner and all sections.
- Send section with real selected files listed (after Batch with file picker).
- Peers section showing a discovered or manually added peer (Batch 5+).
- Incoming transfer confirmation card in its active (accept/reject) state.
- Short GIF of a full loopback/LAN transfer with the transfer log updating live.

## Notes

- Keep the disabled/"Coming later" labels until the matching feature is real.
- No screenshot should ever expose private IPs, MAC addresses, or hostnames —
  use placeholder / redacted values in published images.
