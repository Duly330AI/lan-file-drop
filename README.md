# LAN File Drop

[![CI](https://github.com/Duly330AI/lan-file-drop/actions/workflows/ci.yml/badge.svg)](https://github.com/Duly330AI/lan-file-drop/actions/workflows/ci.yml)

A safe, boring LAN-only file transfer app for trusted devices — no cloud, no SMB, no admin rights, no credentials.

> **MVP status:** Early scaffold. Core validation, UI manual peer validation,
> an explicit UI-triggered bounded manual peer probe, file picker preview,
> send-readiness checks, outgoing draft review, explicit checksum / manifest
> preparation, a hardened local manual peer transfer path in Networking, and
> controlled App send/receive wire-up exist. There is still no LAN discovery,
> real two-PC validation, packaging/release flow, or production-readiness claim.

## Safety

This project is designed to be boring on purpose. It intentionally avoids anything that touches system-level trust, credentials, or remote execution.

- No admin rights required
- No SMB / Windows network shares
- No PowerShell Remoting
- No credentials or accounts
- No WLAN key / Windows key / password access
- No command execution or remote shell
- Local network only
- Receiver confirmation required
- User-selected files only
- Networking path validates destination names, prevents silent overwrite, and verifies checksums

## Planned MVP Features

- Discover or manually address a trusted device on the same local network
- Select one or more files to send from the sending device
- Show an explicit confirmation prompt on the receiving device before any file is written to disk
- Transfer the file(s) over a plain local network connection
- Verify integrity of received files (checksum) after transfer completes
- Save received files into a dedicated, sanitized output folder

## Current MVP Status

What works now:

- Core domain models for transfer requests, peers, and manifests
- Checksum and transfer manifest logic
- Local loopback transfer prototype
- Avalonia UI shell with controlled manual peer transfer controls
- Manual peer endpoint validation in Core
- Manual peer input validation in the UI, with text changes clearing the stored validated endpoint
- Explicit `Probe connection` UI action using the stored validated `ManualPeerEndpoint`
- Bounded manual peer connection probe in Networking; probe-only, with no file send or transfer start
- Explicit file picker preview in the UI; it shows selected file count, total size, file names, and file sizes only
- Send readiness UI summarizing peer, selected-file, manifest, and send status
- Outgoing transfer draft/review skeleton using safe preview metadata only
- Explicit checksum and manifest preparation for selected files; nothing is sent
- Networking manual peer sender/receiver path with explicit receiver confirmation, reject-without-write behavior, checksum verification, and no silent overwrite
- Explicit receive-folder picker and one-shot App receiver start; receive folder path is stored internally and not displayed
- Incoming transfer confirmation in the App with Accept/Reject enabled only while a request is pending
- Explicit `Send prepared transfer` App action using the stored validated peer and prepared manifest

Not implemented yet:

- LAN discovery
- Real two-PC transfer test
- Polished production workflow
- Packaging/release flow
- Production readiness

## Non-Goals

- No cloud sync or internet/WAN transfer
- No NAS or general-purpose file server replacement
- No Windows network shares (SMB) integration
- No user accounts, authentication, or credential storage
- No command execution, scripting, or remote shell capability
- No silent/background transfers — every transfer requires explicit receiver confirmation

## Architecture Overview

The solution is split so that the transfer logic can be tested independently of any UI or networking transport:

- **LanFileDrop.Core** — domain models, transfer manifest, checksum/integrity logic. No UI, no sockets.
- **LanFileDrop.Networking** — discovery and transport concerns, built on top of Core's domain types.
- **LanFileDrop.App** — Avalonia desktop UI, wires Core and Networking together.
- **Tests** — one test project per library (`LanFileDrop.Core.Tests`, `LanFileDrop.Networking.Tests`).

See [docs/architecture.md](docs/architecture.md) for details.

## Development Stack

- C# / .NET 10 (`net10.0`)
- Avalonia UI for the desktop GUI
- xUnit for tests
- Windows portable MVP first; cross-platform potential later via Avalonia

## Roadmap

- **Batch 0** — repo scaffold, solution, docs ✅
- **Batch 1** — core domain models + tests ✅
- **Batch 2** — transfer manifest / checksum logic + tests ✅
- **Batch 3** — local loopback transfer prototype ✅
- **Batch 4** — minimal Avalonia UI shell ✅
- **Batch 5A** — manual peer endpoint validation in Core ✅
- **Batch 5B** — manual peer validation wired into the Avalonia UI ✅
- **Batch 5C** — documentation update for manual peer validation ✅
- **Batch 5D** — manual peer connection plan and networking safety contract ✅
- **Batch 5E** — bounded manual peer connection probe in Networking ✅
- **Batch 5F** — documentation update for manual peer probe status ✅
- **Batch 5G** — explicit UI-triggered manual peer probe without transfer ✅
- **Batch 6A** — file picker preview without sending ✅
- **Batch 6B** — documentation update for file picker preview ✅
- **Batch 6C** — selected-file preview hardening ✅
- **Batch 7A** — send readiness UI skeleton without transfer ✅
- **Batch 8A** — outgoing transfer draft and receiver confirmation skeleton without transfer ✅
- **Batch 9A** — explicit checksum and manifest preparation without sending ✅
- **Batch 10A** — local manual peer transfer path in Networking with receiver confirmation ✅
- **Batch 11A** — controlled App wire-up for manual peer transfer, no discovery ✅
- **Batch 11 next** — App polish, manual smoke hardening, and real two-PC validation remain later
- **Later** — LAN discovery, packaging/release, and production-readiness hardening

## Documentation

- [docs/project-charter.md](docs/project-charter.md) — goal, target users, non-goals, portfolio intent
- [docs/architecture.md](docs/architecture.md) — project separation and responsibilities
- [docs/security.md](docs/security.md) — safety model in detail
- [docs/test-plan.md](docs/test-plan.md) — testing approach across batches

## License

MIT — see [LICENSE](LICENSE).
