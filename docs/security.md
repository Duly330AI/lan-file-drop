# Security & Safety Model

LAN File Drop is deliberately scoped to be a low-risk, "boring" application. This document states explicitly what the project does and does not do, so the safety model is easy to audit.

## What this project will do

- Run as a normal, unprivileged desktop user process (no admin rights required).
- Only transfer files that the sending user explicitly selects.
- Only write received files after the receiving user explicitly confirms the transfer.
- Only operate on the local network segment the device is already connected to.
- Sanitize destination filenames and paths before writing any received file to disk (planned; see roadmap).
- Verify integrity (checksum) of received files after transfer (planned; see roadmap).

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

Networking now includes a bounded manual peer connection probe. It accepts
`ManualPeerEndpoint`, not raw string input from the UI, performs one bounded
`TcpClient` connection attempt, and disposes the client immediately after the
probe. The App can invoke it only from an explicit user click using the stored
validated endpoint. It does not send files, start transfers, or perform receiver
confirmation.

The manual connection path must not use DNS, must not scan, must not create
retry storms, must not run background probing, and must not use broadcast or
multicast.

Validation alone must never start a connection or transfer. Receiver confirmation
is still required before accepting any transfer, destination file paths and names
must remain sanitized, and checksum verification remains required after transfer.

## Current selected-file preview

The App has an explicit `Select files...` action that opens the platform file
picker only after the user clicks it. The preview stores and displays selected
file names and sizes only. It does not display full local paths by default, does
not read file contents, does not compute checksums, does not scan folders, does
not start a transfer, and does not send files. `Send` remains disabled.

## Current send readiness display

The App can display readiness checks for peer state, selected-file state, and
transfer status. These checks are presentation-only. They do not read file
contents, compute checksums, start transfer, send files, perform receiver
confirmation, or add LAN discovery.

## Reporting concerns

As this is a portfolio project, please open a GitHub issue for any safety or security concern rather than a private disclosure — there is no production deployment or user data at stake.
