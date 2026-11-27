# WinBackup

Windows backup utility

![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)
![Framework](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-GPL--3.0-green)
![Status](https://img.shields.io/badge/status-in%20development-orange)

## Overview

WinBackup is a portable backup and synchronization application for 64-bit Windows. It stays responsive while it works, handles long file names, carries on when a single file fails, and reports on everything it did.

> **Note:** WinBackup is in early development. Test it on copies of your data and use Preview before relying on a profile.

## Contents

- [Features](#features)
- [Copy Modes](#copy-modes)
- [Options](#options)
- [Safety](#safety)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Portable Operation](#portable-operation)
- [Limitations](#limitations)
- [Building](#building)
- [Documentation](#documentation)
- [Author](#author)
- [License](#license)

## Features

- **Profiles** organized in folders, with enable and disable, copy, and rename
- **Multiple source and destination folders** in each profile, with drag and drop
- **Preview** of every planned action with comparison totals, and the ability to change individual items before running
- **Background operation** with progress, pause, and cancel
- **Long file name support**
- **Reports** for every run, with error filtering
- **Scheduler** to run profiles once, at intervals, daily, weekly, monthly, or at application start
- **Email notification** of results
- **System tray** operation
- **Light and dark themes**

## Copy Modes

| Mode | Description |
|---|---|
| Mirror | Makes the destination an exact copy of the source, removing items that no longer exist in the source |
| Update | Copies new and newer files to the destination without deleting anything |
| Backup | Copies to dated folders or ZIP files and keeps a set number of generations |
| Sync | Brings two sides up to date in both directions, with conflict handling |

## Options

| Area | Options |
|---|---|
| Filters | Include or exclude files and folders by name, path, or wildcard |
| Development files | Optionally skip build output, Visual Studio working files, and Git data, and remove them from a mirrored destination |
| Overwriting | Automatic, newer, different, always, or never |
| Data protection | Send removed and replaced files to the Recycle Bin or a version folder, or delete them |
| Reliability | Retry failed items and optionally verify copied files |
| Backup | Dated folder or ZIP file, name pattern, generations, and compression |
| Sync conflicts | Skip and report, newer wins, source wins, or destination wins |
| Schedule | Time, interval, days, missed runs, and run at application start |
| Email | SMTP server, recipients, and when to send |
| Windows | Always on top, taskbar visibility, and remembered window positions |
| Tray | Close to tray, start minimized, status icon, and notifications |
| Logging | Retention of logs and reports |

## Safety

- Nothing is changed when a source folder is unavailable
- A configurable safeguard stops runs that would delete an unusually large number of files
- Existing files are replaced only after the new copy has been written successfully
- Folders that cannot be read are never treated as deleted
- Links and junctions are not followed or deleted

## Requirements

- Windows, 64-bit
- .NET 10 Desktop Runtime

## Getting Started

1. Place the WinBackup folder in a location you can write to and run `WinBackup.exe`.
2. Choose **New Profile**, enter a name, and pick a copy mode.
3. Add source and destination folders and save.
4. Choose **Preview** to check the planned actions, then **Run**.

## Portable Operation

WinBackup runs from its own folder and writes nothing to the registry.

| Folder | Contents |
|---|---|
| `config` | Settings, profiles, and history, created when the application runs |
| `logs` | Daily logs and run reports |
| `docs` | Help |

The saved email password is tied to the Windows user account and must be entered again after moving to another computer.

## Limitations

- Scheduled runs happen only while WinBackup is running
- Files locked by other programs are reported and not copied
- The Recycle Bin is available only for local fixed drives and shorter paths; use a version folder otherwise
- File permissions and alternate data streams are not copied
- ZIP passwords and split archives are not available
- Email supports STARTTLS, not implicit TLS

## Building

1. Install Visual Studio with the .NET desktop development workload and the .NET 10 SDK.
2. Open `source/WinBackup.sln`.
3. Select **Release** and **x64**, then build or publish.

## Documentation

Detailed help is in the `docs` folder of each release and opens from the Help menu or with F1.

## Author

Ftuoil Xelrash

## License

WinBackup is licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE) for details.
