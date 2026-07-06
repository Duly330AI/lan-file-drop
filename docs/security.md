# Security & Safety Model

LAN File Drop is deliberately scoped to be a low-risk, "boring" application. This document states explicitly what the project does and does not do, so the safety model is easy to audit.

## What this project will do

- Run as a normal, unprivileged desktop user process (no admin rights required).
- Only transfer files that the sending user explicitly selects.
- Only write received files after the receiving user explicitly confirms the transfer.
- Only operate on the local network segment the device is already connected to.
- Sanitize destination filenames and paths before writing any received file to disk.
- Verify integrity (checksum) of received files after transfer.

## What this project will explicitly not do

- **No admin-only design.** The app must run and function fully as a standard user.
- **No SMB dependency.** It does not create, mount, or rely on Windows network shares.
- **No PowerShell Remoting** or any other remote command/scripting execution.
- **No credential handling.** No accounts, passwords, tokens, or stored secrets of any kind.
- **No WLAN key, Windows product key, or password extraction** — the app has no reason to ever touch these and will not.
- **No command execution feature.** The app cannot be used to run arbitrary commands on either device.
- **No remote shell** of any kind, in either direction.
- **No silent transfers.** A transfer without explicit receiver confirmation is treated as a bug.
- **No WAN/internet transfer.** If a transfer would need to leave the local network, it is out of scope.
- **No firewall or network configuration changes performed by the app itself** beyond what Windows' own "allow this app on private networks" prompt requires on first run.

## Expected first-run behavior

The first time the app opens a network listener, Windows Defender Firewall will likely prompt the user to allow the app on private/public networks. This is expected, standard behavior for any LAN-facing app and is not something the app configures itself — the user retains full control via the standard Windows prompt.

## Current manual peer validation

Manual peer endpoint validation is local input parsing only. It does not perform
DNS lookup, LAN discovery, socket creation, file access, or transfer startup.
Validation alone does not probe. In the UI, probing requires an explicit
`Probe connection` click after a valid `ManualPeerEndpoint` exists; text changes
clear the stored validated endpoint and disable probing.

## Manual peer networking contract

Networking includes a bounded manual peer connection probe. It accepts
`ManualPeerEndpoint`, not raw string input from the UI, performs one bounded
`TcpClient` connection attempt, and disposes the client immediately after the
probe. The App can invoke it only from an explicit user click using the stored
validated endpoint. The probe does not send files, start transfers, or perform
receiver confirmation.

Networking also includes the first local manual peer transfer path. The sender
requires both a validated `ManualPeerEndpoint` and an explicit
`PreparedOutgoingTransferManifest`; there is no public send API that accepts raw
UI text or sends without a prepared manifest. The receiver reads request
metadata first and calls an explicit confirmation callback; the sender sends
payloads only after the receiver accepts. Rejecting a request writes no files.

The receiver validates file names (rejecting path separators, traversal
segments, and Windows reserved device names such as `CON`/`NUL`/`COM1`), keeps
destination writes inside the receiver directory, and refuses existing
destination files instead of overwriting them. Each accepted payload is streamed
into a private temporary file in the destination directory while its SHA-256 is
computed, so a payload is never fully buffered in memory. A temp file is only
promoted to its final, sanitized name after its declared size and checksum
verify. For a multi-file transfer, promotion is all-or-nothing: if a later
promotion fails, the final files this transfer already created are rolled back
and the temp files are deleted on a best-effort basis, so a partial transfer
never leaves final or corrupt files behind. Structurally readable but invalid
requests (bad names, duplicates, oversize) receive a clean rejection status
rather than a dropped connection, and failure reasons are generic and never
include local paths. The current App UI is not wired to this transfer path yet:
it does not listen, accept, send, or write transfer files.

The manual connection path must not use DNS, must not scan, must not create
retry storms, must not run background probing, and must not use broadcast or
multicast.

Validation alone must never start a connection or transfer. Receiver confirmation
is required before accepting any transfer, destination file paths and names must
remain sanitized, and checksum verification is required before a received file is
written by the current Networking path.

## Current selected-file preview

The App has an explicit `Select files...` action that opens the platform file
picker only after the user clicks it. The preview stores and displays selected
file names and sizes only. It does not display full local paths by default, does
not read file contents during selection, does not compute checksums during
selection, does not scan folders, does not start a transfer, and does not send
files. `Send` remains disabled.

## Current send readiness display

The App can display readiness checks for peer state, selected-file state, and
transfer status. These checks are presentation-only. They do not read file
contents, compute checksums, start transfer, send files, perform receiver
confirmation, or add LAN discovery.

## Current outgoing transfer draft

The App can prepare a review-only outgoing transfer draft from safe preview
metadata: validated peer display, selected file names, and selected file sizes
when available. Drafts do not include full local paths, file handles, file
contents, or checksums. Preparing or clearing a draft does not send files, start
transfer, perform receiver confirmation, create a listener, or add LAN
discovery.

## Current checksum and manifest preparation

The App retains selected file handles only for the current explicit selection so
the user can later click `Prepare manifest`. That action is the only current UI
path that reads selected file contents: it opens selected file streams, computes
SHA-256 checksums, and prepares manifest metadata. It does not display or log
full local paths, write files, send files, start transfer, perform receiver
confirmation, or add LAN discovery. Selection changes, clear actions, and window
close dispose retained selection handles.

## Reporting concerns

As this is a portfolio project, please open a GitHub issue for any safety or security concern rather than a private disclosure — there is no production deployment or user data at stake.
