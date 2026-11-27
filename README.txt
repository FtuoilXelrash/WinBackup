WINBACKUP
=========

Windows backup utility

Platform:  Windows x64
Framework: .NET 10
License:   GPL-3.0
Version:   1.0.0


OVERVIEW
--------

WinBackup is a portable backup and synchronization application for 64-bit
Windows. It stays responsive while it works, handles long file names, carries
on when a single file fails, and reports on everything it did.

Note: Use Preview to check what a new or changed profile will do before you
run it.


CONTENTS
--------

- Features
- Copy Modes
- Options
- Safety
- Requirements
- Getting Started
- Portable Operation
- Limitations
- Building
- Documentation
- Author
- License


FEATURES
--------

- Profiles organized in folders, with enable and disable, copy, and rename
- Multiple source and destination folders in each profile, with drag and drop
- Preview of every planned action, color coded, with comparison totals and the
  ability to change individual items before running
- Fast comparison by name, size, and date, with a clear message when
  everything is up to date
- Background operation with progress, pause, and cancel
- Long file name support
- Reports for every run, with error filtering
- Scheduler to run profiles once, at intervals, daily, weekly, monthly, or at
  application start
- Email notification of results
- System tray operation
- Light and dark themes


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
    Brings two sides up to date in both directions, with conflict handling.


OPTIONS
-------

Filters
    Include or exclude files and folders by name, path, or wildcard.

Development files
    Optionally skip build output, Visual Studio working files, and Git data,
    and remove them from a mirrored destination.

Overwriting
    Automatic, newer, different, always, or never.

Data protection
    Send removed and replaced files to the Recycle Bin or a version folder, or
    delete them.

Reliability
    Retry failed items and optionally verify copied files.

Backup
    Dated folder or ZIP file, name pattern, generations, and compression.

Sync conflicts
    Skip and report, newer wins, source wins, or destination wins.

Schedule
    Time, interval, days, missed runs, and run at application start.

Email
    SMTP server, recipients, and when to send.

Windows
    Always on top, taskbar visibility, and remembered window positions.

Tray
    Close to tray, start minimized, status icon, and notifications.

Logging
    Retention of logs and reports.


SAFETY
------

- Nothing is changed when a source folder is unavailable
- A configurable safeguard stops runs that would delete an unusually large
  number of files
- Existing files are replaced only after the new copy has been written
  successfully
- Folders that cannot be read are never treated as deleted
- Links and junctions are not followed or deleted
- Filtered files are left alone in the destination unless you choose to remove
  skipped development files


REQUIREMENTS
------------

- Windows, 64-bit
- .NET 10 Desktop Runtime


GETTING STARTED
---------------

1. Place the WinBackup folder in a location you can write to and run
   WinBackup.exe.
2. Choose New Profile, enter a name, and pick a copy mode.
3. Add source and destination folders and save.
4. Highlight the profile, choose Preview to check the planned actions, then
   Run.


PORTABLE OPERATION
------------------

WinBackup runs from its own folder and writes nothing to the registry.

config
    Settings, profiles, and history, created when the application runs.

logs
    Daily logs and run reports.

docs
    Help.

The saved email password is tied to the Windows user account and must be
entered again after moving to another computer.


LIMITATIONS
-----------

- Scheduled runs happen only while WinBackup is running
- Files locked by other programs are reported and not copied
- Files that another program is writing to may be copied in an unusable state,
  so close databases, mail programs, and virtual machines first
- Preview compares names, sizes, and dates, not file contents; verification
  applies to files copied during a run
- The Recycle Bin is available only for local fixed drives and shorter paths;
  use a version folder otherwise
- File permissions and alternate data streams are not copied
- ZIP passwords and split archives are not available
- Email supports STARTTLS, not implicit TLS


BUILDING
--------

1. Install Visual Studio with the .NET desktop development workload and the
   .NET 10 SDK.
2. Open source/WinBackup.sln.
3. Select Release and x64, then build or publish.


DOCUMENTATION
-------------

Detailed help is in the docs folder of each release and opens from the Help
menu or with F1.


AUTHOR
------

Ftuoil Xelrash


LICENSE
-------

WinBackup is licensed under the GNU General Public License v3.0. See the
LICENSE file for details.
