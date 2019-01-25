**AtlasUpdater**

This is a modification and re-write from Bottswana's Ark Updater located at: https://github.com/Bottswana/ARKUpdater in order to work with the Atlas dedicated server.

AtlasUpdater is a Console Application (in c#) written for the purpose of performing updates and backups for Atlas Dedicated Servers.
It is intended for Server Admins who wish to automate their backup and updating processes, without using Server Manager GUIs.
AtlasUpdater is released publically under the MIT License, and is free to use. For more information on the MIT License, see [LICENSE].

## Features
-- Console only (No GUI)
- Auto restart of crashed Grid servers
- Auto update of server
- Auto backup of Grid servers using compression
- RCON messaging for warning users of restart/update.
- Only update when player count is less than a value (optional)

## Setup
To setup your servers to work with AtlasUpdater, perform the following.

YOU MUST HAVE .net FRAMEWORK 4.6.1 Installed!

1) Shutdown your servers

2) Compile/Use a precompiled build of AtlasUpdater, and extract to a folder with write permissions.

3) Modify the settings.json file to your system ... add your servers and path information to the configuration (see section below).

4) Start AtlasUpdater and allow it to launch your servers for you

5) Revel in the glory of your auto updating servers. You are such a good server admin!


## Important Considerations
On first launch of AtlasUpdater, your servers should not be running (Or AtlasUpdater will launch them again for you!)
ARKUpdater writes a PID file to the server directory for each server it is administering when it launches a server, so you can restart ARKUpdater without needing to restart your servers.

This means that AtlasUpdater needs to launch your servers to work. You can also edit this PID file with the ID of the process before launching as a fallback.

AtlasUpdater will respawn any servers that close while it is running if it does not expect them to close. This means if you need to shut down a server, you need to stop ARKUpdater or it will relaunch the server for you.

## Configuration Options - in the setup.json file

**Global Configuration Options:**

- PostponeUpdateWhenPlayersHigherThan

	When the server player count is higher than this value, automatic updates will not take place. When the count of players drops below this value, auto updates will resume.
	Will not work if the player count rises above this value after an update has been triggered.
	Set to 0 to disable this feature

- UpdateWarningTimeInMinutes

	The amount of time to wait before starting an update. A notification will be sent by RCON for each minute, so if 10 minutes is set here, 10 notifications will be sent to the server before the update starts.
	Set to 0 to start an update immediately on detection (Not recommended!)

- UpdatePollingInMinutes

	How often to poll the steam network for a Atlas update. It is not recommended to set this too frequently, as this may cause unintended side effects.

- LogLevel

	The level of information to display in console and in the log file. The options are:
	Debug, Info, Success, Warning, Error.
	The recommended value is Info.

- SteamCMDPath

	The path to the SteamCMD executable. This should not include the name of the executable, just the folder that contains it. For example: "C:\\SteamCMD".
	For Windows, double escape backslashes in the path. So C:\Windows should be C:\\Windows.
	
- SteamUpdateScript

	Path to the SteamCMD update script for this server. If you are not currently using an update script, an example can be found in the resources folder.
	Note: This script must end with the quit command, or AtlasUpdater will hang waiting for SteamCMD to exit. Check the example script for a list of suggested commands.	

- GameServerPath

	The path to the root of the gameserver. Should contain the 'Engine' and 'ShooterGame' folders.

- ShowSteamUpdateInConsole

	If true, output from SteamCMD will be shown in the AtlasUpdater console. If false, output will be supressed.
	Default is true.

- UseServerNameInINIFile

	If true, ?ServerName= will be supressed in the commandline. If false, it will be supplied. This is useful if your server name contains spaces or special characters, in which case you should set it in your GameUserSettings.ini file, and set this to false. You must still supply a server name, as this is used to reference the server within the console itself, for log messages ect.

- EnableBackup

	If the automatic backup process should be enabled or disabled.

- BackupIntervalInMinutes

	How often to execute the server backup

- NumberOfBackupsToKeepPerServer

	How many backups should be held in the backup directory at one time.

- Messages

	These are the customisable messages sent via RCON to the server. Only the Backup message is optional. If you do not want the Backup message, leave this field blank.
	In UpdateBroadcast, {0} will be replaced with a minute count, and is mandatory.


**Server Configuration Options: - one entry for each grid server**  


- BackupDirectory

	The folder to store compressed backup archives for this server. This should be a unique path for each server instance. Leave this blank if you are not using the backup feature.

	
- GameServerPIDPath

	A Path to a directory that holds a pid file for each server grod that you start. I.E. - for a 2x2 Grid you will have 4 servers  	running so you will need 4 directories.	"E:\\AtlasUpdater\\AtlasPIDFiles\\A1" ,  "E:\\AtlasUpdater\\AtlasPIDFiles\\A2", 
	"E:\\AtlasUpdater\\AtlasPIDFiles\\B1", "E:\\AtlasUpdater\\AtlasPIDFiles\\B2" 

- GameServerName

	The name of this server. See 'UseServerNameInINIFile' above.
	This is required.

- GameServerMap

	The map for this server to run. Always 'Ocean'.

- MaxPlayers

	The amount of slots for this server

- QueryPort

	Steam Query port. Usually 27015 unless occupied with another server

- RCONPort

	The port for RCON. RCON must be enabled for AtlasUpdater to work, but you can block/not allow this port through your firewall if you do not want RCON, as it is used over localhost by AtlasUpdater.

- Port

	The gameserver port. There will be one for each Grid Server

- ServerPassword

	The password required to join this gameserver. Leave blank if your server is public.

- ServerAdminPassword

	The password for admin/cheat commands and RCON. This is required.

- ServerPVE

	If this server should be PvE or not. True for PvE, False for PvP.

- ServerParameters

	Any launch parameters you wish to set on the server. You can set as many options as you want here. You should use a comma to indicate a new line inside this object, but the last entry must not contain a comma.
	Entries are translated into commandline format. For example: "NoTributeDownloads": "true" will become ?NoTributeDownloads=true. No validation is done on these entries.
	An example of multiple commands in this section would be:

    "ServerParameters": { 
        "NoTributeDownloads": "true",
        "ShowMapPlayerLocation": "false",
        "HarvestHealthMultiplier": "2.0"
    }


Note: Unless otherwise specified, all fields are required in settings.json.
If leaving a field blank, place empty speech marks, for example: "ServerPassword": ""

## Work In Progress

- Better validation for settings.json.
- Cleanup some not needed setup flags and settings in the .jason file
- Port everything over to a proper windows GUI

## Mentions

ConsolePlus - Console Formatting Library for c#
http://www.codeproject.com/Tips/684225/Console-Coloring-with-ConsolePlus

JSON.Net - Json Parsing for .Net
http://www.newtonsoft.com/json

SteamKit2 - .Net Library for communicating with Steam3
https://github.com/SteamRE/SteamKit

SevenZipSharp - C# Wrapper for 7z
https://sevenzipsharp.codeplex.com/


This application contains code from part of the 7-zip project. Licensed under the GNU LGPL License.

For more information on 7-zip, their website is located at: http://www.7-zip.org
