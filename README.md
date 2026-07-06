# LAN File Drop

[![CI](https://github.com/Duly330AI/lan-file-drop/actions/workflows/ci.yml/badge.svg)](https://github.com/Duly330AI/lan-file-drop/actions/workflows/ci.yml)

A safe, boring LAN-only file transfer app for trusted devices — no cloud, no SMB, no admin rights, no credentials.

> **MVP status:** Early scaffold / not functional yet.

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
- Planned: sanitized output paths and integrity check after transfer

## Planned MVP Features

- Discover or manually address a trusted device on the same local network
- Select one or more files to send from the sending device
- Show an explicit confirmation prompt on the receiving device before any file is written to disk
- Transfer the file(s) over a plain local network connection
- Verify integrity of received files (checksum) after transfer completes
- Save received files into a dedicated, sanitized output folder

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
- **Batch 4** — minimal Avalonia UI shell ✅ *(this batch)*
- **Batch 5** — LAN discovery / manual IP fallback
- **Batch 6** — two-PC manual test hardening

## Documentation

- [docs/project-charter.md](docs/project-charter.md) — goal, target users, non-goals, portfolio intent
- [docs/architecture.md](docs/architecture.md) — project separation and responsibilities
- [docs/security.md](docs/security.md) — safety model in detail
- [docs/test-plan.md](docs/test-plan.md) — testing approach across batches

## License

MIT — see [LICENSE](LICENSE).
