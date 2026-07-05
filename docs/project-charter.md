# Project Charter

## Goal

Provide a simple way to send files between two trusted Windows devices on the same local network — without relying on cloud services, network shares, or any elevated system access. Think "a local AirDrop equivalent," but explicitly scoped to stay safe and boring.

## Target Users

- Individuals who want to move files between their own devices on a home or office LAN without setting up file shares, cloud sync, or USB sticks.
- Developers and reviewers looking at this repository as a portfolio example of a small, safety-conscious desktop application with a clean core/UI separation.

## MVP

The minimum viable product covers exactly one flow:

1. A user on Device A selects one or more files to send.
2. Device A locates Device B on the same local network (manual IP entry first, LAN discovery later).
3. Device B's user sees an explicit confirmation prompt describing the incoming file(s) and must accept before anything is written to disk.
4. The file(s) transfer over the local network.
5. Device B verifies the integrity of the received file(s) and reports success or failure.

Everything outside this flow is out of scope for the MVP.

## Non-Goals

- Not a cloud file-sharing product.
- Not a replacement for SMB shares, NAS systems, or sync tools like OneDrive/Dropbox.
- Not a general-purpose network administration or remote-management tool.
- Not intended to work across the public internet.

## Portfolio Intent

This repository is maintained as a public portfolio project. It is meant to demonstrate:

- Deliberate scoping of a "dangerous-sounding" feature category (LAN file transfer) into something safe and well-bounded.
- A clean separation between core domain logic, networking, and UI, each independently testable.
- Careful, incremental delivery (see the batch roadmap in [README.md](../README.md)).
