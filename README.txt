WINBACKUP
=========

Windows backup utility

Platform:  Windows x64
Framework: .NET 10
License:   GPL-3.0
Status:    In development


OVERVIEW
--------

WinBackup is a portable backup and synchronization application for 64-bit
Windows. It is designed to stay responsive while it works, handle long file
names, carry on when a single file fails, and report accurately on everything
it did.

Note: WinBackup is in early development. The features below describe what is
planned and are not yet available.


CONTENTS
--------

- Planned Features
- Copy Modes
- Options
- Requirements
- Portable Operation
- Documentation
- Author
- License


PLANNED FEATURES
----------------

- Profiles to store, organize, and reuse backup jobs
- Multiple source and destination folders in each profile
- Preview of every planned action, with the ability to change individual items
  before running
- Background operation without blocking dialogs, with pause and cancel
- Long file name support
- Reports for every run, with error filtering
- Scheduler to run profiles automatically
- Email notification of results
- System tray operation and a compact status window


COPY MODES
----------

Mirror
    Makes the destination an exact copy of the source, removing items that no
    longer exist in the source.

Update
    Copies new and newer files to the destination without deleting anything.

Backup
    Copies to dated folders or ZIP files and keeps a set number of
    generations.

Sync
    Brings two folders up to date in both directions.


OPTIONS
-------

Filters
    Include or exclude files and folders by name, path, or wildcard.

Data protection
    Send removed and replaced files to the Recycle Bin or to a versioned
    folder.

Reliability
    Retry failed files and optionally verify copied files.

Safeguards
    Stop when a source is missing or an unusually large number of files would
    be deleted.

Windows
    Always on top, taskbar visibility, and remembered window positions.

Tray
    Close to tray, start minimized, and status icon.

Appearance
    Light and dark themes.


REQUIREMENTS
------------

- Windows, 64-bit
- .NET 10 Desktop Runtime


PORTABLE OPERATION
------------------

WinBackup runs from its own folder. Settings are saved in a config folder
beside the application, which is created when the application runs.


DOCUMENTATION
-------------

End user documentation is included in the docs folder of each release.
Detailed help is also available from the Help menu in the application.


AUTHOR
------

Ftuoil Xelrash


LICENSE
-------

WinBackup is licensed under the GNU General Public License v3.0. See the
LICENSE file for details.
