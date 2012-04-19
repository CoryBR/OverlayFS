OverlayFS Readme
===================

OverlayFS is a user mode filesystem which creates an in-memory overlay of a folder.
Changes are tracked per user, and erased within a minute of a user logging out.
OverlayFS does not support creating subdirectories in memory or creating a file in a subdirectory, but does support
creating files in the root of the destination and supports modifying existing files within subdirectories.


System Requirements
===================
Microsoft .NET Framework 3.0
Dokan user mode file system driver 0.6.0 <http://dokan-dev.net/en/> (higher versions require an updated DokanNet.dll to be placed in the program directory)


Configuration
===================
OverlayFS is configured through settings.xml located in the installation folder. Multiple folders can be specified within this file.
It is preconfigured for a standard School Dynamics installation. The OverlayFS service must be restarted after a configuration change.
Please ensure no users are accessing the destination directory during a service restart, as they will receive file system error messages.

settings.xml example:

<root>
  <overlay source="C:\School Database" destination="C:\School Database2" logLevel="1"/>
</root>


Log Levels
===================
0 - Errors only
1 - Basic information
2 - User status
3 - Debugging information

A higher log level includes all messages associated with lower levels.