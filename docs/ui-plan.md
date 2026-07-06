# UI Plan & Screenshot Plan

Status: Batch 11A wires the Avalonia UI shell to the hardened manual peer
transfer path in a controlled way. There is still **no DNS, LAN discovery,
broadcast, multicast, background probing, auto-accept, broad folder sending, or
drag/drop**. Manual peer validation stores a `ManualPeerEndpoint`; the optional
`Probe connection` click remains probe-only and starts no transfer. `Select
files...` previews selected file names and sizes only. `Prepare manifest` is an
explicit user action that reads selected file streams to calculate SHA-256
metadata. `Send prepared transfer` is a separate explicit click and is enabled
only when the validated peer, selected files, and prepared manifest still match.
Receiving requires an explicit receive-folder selection, explicit one-shot
receiver start, and explicit Accept before final files are written.

## Current shell sections

1. **Safety banner** — always-visible status line plus the safety bullet list.
2. **Local device** — placeholder display name and `Not connected · UI shell only` status.
3. **Send files** — selected-file preview and explicit send flow. `Select files...` opens
   the platform file picker only from explicit user click, then shows selected
   file count, total size, file names, and file sizes. It does not display full
   local paths by default, read file contents, compute checksums, scan folders,
   or start transfer during selection. The Send readiness area summarizes peer
   state, selected file state, manifest state, and send state. `Prepare transfer draft` creates a
   review-only draft from the validated peer display and selected file metadata;
   clearing or changing peer/files clears the draft and manifest. `Prepare
   manifest` explicitly reads the selected files, computes SHA-256 checksums,
   prepares manifest metadata, and still sends nothing. `Send prepared transfer`
   uses the stored validated endpoint and prepared manifest, creates outgoing
   stream entries from the current `IStorageFile` handles, and opens those
   streams only through the explicit send action.
4. **Peers** — manual peer input plus `Validate peer` and `Probe connection`
   buttons. `Validate peer` runs local endpoint validation only. A valid
   endpoint is stored as a `ManualPeerEndpoint`; text changes clear that stored
   endpoint and disable probing. `Probe connection` is enabled only after a
   valid endpoint exists, runs only from explicit user click, uses the stored
   validated endpoint rather than raw text, sends no files, starts no transfer,
   performs no receiver confirmation, and does not implement LAN discovery.
5. **Incoming transfer** — controlled receiver flow. `Select receive folder`
   opens the platform folder picker only from explicit user click. The App uses
   `TryGetLocalPath` only for this receive destination, stores the local path
   internally, and displays only a safe folder label. `Start receiver` requires
   a selected receive folder and valid port, starts one one-shot TCP receiver,
   and does not auto-restart. `Stop receiver` cancels the pending receive.
   Incoming request metadata shows request id, file count, total size, safe file
   names, and checksum/manifest presence, never full paths. `Accept` / `Reject`
   are enabled only while a request is awaiting an explicit decision.
6. **Transfer log** — static sample / empty-state entries.

## Screenshots / GIFs to capture later

- Full window at startup showing the safety banner and all sections.
- Send section with preview-only selected files listed. Use safe sample names;
  do not show full local paths or real personal data.
- Peers section showing a validation-only manual peer with neutral placeholder
  values. Do not show real private IPs, MAC addresses, or hostnames.
- Incoming transfer confirmation card in its active (accept/reject) state.
- Short GIF of a full manual loopback transfer with the transfer log updating live.

## Notes

- Keep labels honest: the current App supports controlled manual transfer, but
  LAN discovery, polished workflow, packaging, and production readiness remain
  later.
- No screenshot should ever expose private IPs, MAC addresses, or hostnames —
  use placeholder / redacted values in published images.
