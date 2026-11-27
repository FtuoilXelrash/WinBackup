# WinBackup

Windows backup utility

![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)
![Framework](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-GPL--3.0-green)
![Status](https://img.shields.io/badge/status-in%20development-orange)

## Overview

WinBackup is a portable backup and synchronization application for 64-bit Windows. It is designed to stay responsive while it works, handle long file names, carry on when a single file fails, and report accurately on everything it did.

> **Note:** WinBackup is in early development. The features below describe what is planned and are not yet available.

## Contents

- [Planned Features](#planned-features)
- [Copy Modes](#copy-modes)
- [Options](#options)
- [Requirements](#requirements)
- [Portable Operation](#portable-operation)
- [Documentation](#documentation)
- [Author](#author)
- [License](#license)

## Planned Features

- **Profiles** to store, organize, and reuse backup jobs
- **Multiple source and destination folders** in each profile
- **Preview** of every planned action, with the ability to change individual items before running
- **Background operation** without blocking dialogs, with pause and cancel
- **Long file name support**
- **Reports** for every run, with error filtering
- **Scheduler** to run profiles automatically
- **Email notification** of results
- **System tray** operation and a compact status window

## Copy Modes

| Mode | Description |
|---|---|
| Mirror | Makes the destination an exact copy of the source, removing items that no longer exist in the source |
| Update | Copies new and newer files to the destination without deleting anything |
| Backup | Copies to dated folders or ZIP files and keeps a set number of generations |
| Sync | Brings two folders up to date in both directions |

## Options

| Area | Options |
|---|---|
| Filters | Include or exclude files and folders by name, path, or wildcard |
| Data protection | Send removed and replaced files to the Recycle Bin or to a versioned folder |
| Reliability | Retry failed files and optionally verify copied files |
| Safeguards | Stop when a source is missing or an unusually large number of files would be deleted |
| Windows | Always on top, taskbar visibility, and remembered window positions |
| Tray | Close to tray, start minimized, and status icon |
| Appearance | Light and dark themes |

## Requirements

- Windows, 64-bit
- .NET 10 Desktop Runtime

## Portable Operation

WinBackup runs from its own folder. Settings are saved in a `config` folder beside the application, which is created when the application runs.

## Documentation

End user documentation is included in the `docs` folder of each release. Detailed help is also available from the Help menu in the application.

## Author

Ftuoil Xelrash

## License

WinBackup is licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE) for details.
