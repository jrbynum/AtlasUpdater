using System;
using System.Linq;
using System.Text;
using System.Threading;
using AtlasUpdater.Classes;
using AtlasUpdater.Interfaces;
using System.Collections.Generic;
using System.Windows.Forms;


namespace AtlasUpdater
{
	#region Program Stub
	class Program
	{
        [STAThread]
        static void Main(string[] args)
		{
			var System = new AtlasUpdater();
			try { Console.WindowWidth = 140; } catch ( NotSupportedException ) {}

			// Because a little fun is compulsory
			string[] ConsoleTitles = {"King of the Seas!... I mean Servers", "Your wish is my command... Master", "I never sleep... I'm always Watching..."};
			Console.Title = string.Format("AtlasUpdater: {0}", ConsoleTitles[ (new Random()).Next(0, ConsoleTitles.Length) ]);

			Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
				e.Cancel = true; System.Shutdown();
			};

			System.Run();
		}
	}
	#endregion Program Stub

	#region Global Methods
	static class Helpers
	{
		public static string Base64Decode(string EncodedData)
		{
			var ByteArray = Convert.FromBase64String(EncodedData);
			return Encoding.UTF8.GetString(ByteArray);
		}

		public static string FindLocalEndpoint(System.Net.IPEndPoint remote)
		{
			var testSocket = new System.Net.Sockets.Socket(remote.AddressFamily, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
			testSocket.Connect(remote);

			return ((System.Net.IPEndPoint)testSocket.LocalEndPoint).Address.ToString();
		}

		public static bool IsUnixPlatform()
		{
			int PlatformVersion = (int) System.Environment.OSVersion.Platform;
			return ( (PlatformVersion == 4) || (PlatformVersion == 6) || (PlatformVersion == 128) ) ? true : false;
		}

		public static void ExitWithError()
		{
			Console.WriteLine("Press any key to exit...");
			Console.ReadKey();

			System.Environment.Exit(1);
		}
        public static string GetFileName()
        {
            string path = null;

            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {
                InitialDirectory = @"C:\",
                Title = "Locate JSON Settings File",

                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "json",
                Filter = "json files (*.json)|*.json",
                FilterIndex = 2,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            //Invoke((Action)(() => { saveFileDialog.ShowDialog() }));
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {

                path = openFileDialog1.FileName;
                return path;
            }

            return path;

        }
        public static string GetApplicationVersion()
		{
			System.Reflection.Assembly execAssembly = System.Reflection.Assembly.GetCallingAssembly();
			System.Reflection.AssemblyName name = execAssembly.GetName();
			return string.Format("{0:0}.{1:0}.{2:0} (.NET {3})",
				name.Version.Major.ToString(),
				name.Version.Minor.ToString(),
				name.Version.Build.ToString(),
				execAssembly.ImageRuntimeVersion
			);
		}

		static readonly DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0);
		public static DateTime FromUnixStamp(int secondsSinceepoch)
		{
			return epochStart.AddSeconds(secondsSinceepoch);
		}
		
		public static int ToUnixStamp(DateTime dateTime)
		{
			return (int)( dateTime - epochStart ).TotalSeconds;
		}

		public static int CurrentUnixStamp
		{
			get 
			{
				return (int)( DateTime.UtcNow - epochStart ).TotalSeconds;
			}
		}
	}

	class ServerClass
	{
		public int ProcessID;
		public int MinutesRemaining;

		public int LastUpdated;
		public int LastBackedUp;
		public SettingsLoader.ServerChild ServerData;

		public ServerClass(SettingsLoader.ServerChild Data)
		{
			this.ProcessID = 0;
			this.MinutesRemaining = -1;

			this.LastUpdated = 0;
			this.LastBackedUp = 0;

			this.ServerData = Data;
		}
	}
	#endregion Global Methods

	class AtlasUpdater
	{
		public SettingsLoader ATLASConfiguration;
		public ServerClass[] Servers;
		public ConsoleLogger Log;

		public BackupInterface BackupInt;
		public ServerInterface ServerInt;
		public SteamInterface SteamInt;

		private ManualResetEvent _Sleeper;
		private bool _Running;
        private int numOfGrids;

		public AtlasUpdater()
		{
			this._Sleeper = new ManualResetEvent(false);
			this._Running = true;

			// Initialise Console Logging
			this.Log = new ConsoleLogger(LogLevel.Debug);
			this.Log.ConsolePrint(LogLevel.Info, "AtlasUpdater Starting (Version: {0})", Helpers.GetApplicationVersion());
			
			// Load configuration from settings.json located in current directory
			this.ATLASConfiguration = SettingsLoader.LoadConfiguration("settings.json", Log);
			if( this.ATLASConfiguration == null )
			{//file not found lets try and find one....

                string f = Helpers.GetFileName();

                this.ATLASConfiguration = SettingsLoader.LoadConfiguration(f, Log);
                if (this.ATLASConfiguration == null)
                { //still didnt find a file so exit...
                    Helpers.ExitWithError();
                }
            }

			// Configure Logger
			if( this.ATLASConfiguration.LogLevel.Length > 0 )
			{
				try
				{
					var LogValue = (LogLevel) Enum.Parse(typeof(LogLevel), this.ATLASConfiguration.LogLevel, true);
					this.Log.SetLogLevel(LogValue);
				}
				catch( ArgumentException )
				{
					this.Log.ConsolePrint(LogLevel.Error, "Invalid LogLevel in settings.json, using default (DEBUG)");
				}
			}

			// Configure Servers
			var TempList = new List<ServerClass>();
			foreach( var ServerData in this.ATLASConfiguration.Servers )
			{
				TempList.Add( new ServerClass(ServerData) );
				this.Log.ConsolePrint(LogLevel.Debug, "Loaded server shard '{0}' from configuration", ServerData.GameServerName);
			}

            //convert from list to array
			this.Servers = TempList.ToArray();
            //get the # of grids - this will be the number of server instances that we start
            numOfGrids = TempList.Count;

			// Initialise Interfaces
			if( Helpers.IsUnixPlatform() )
			{
				this.ServerInt = new ServerInterfaceUnix(this);
				this.BackupInt = new BackupInterfaceUnix(this);
				this.SteamInt = new SteamInterfaceUnix(this);
			}
			else
			{
				this.ServerInt = new ServerInterfaceWindows(this);
				this.BackupInt = new BackupInterfaceWindows(this);
				this.SteamInt = new SteamInterfaceWindows(this);
			}

			// Verify path to SteamCMD - exit if it can't be found
			if( !this.SteamInt.VerifySteamPath(this.ATLASConfiguration.SteamCMDPath) )
			{
				this.Log.ConsolePrint(LogLevel.Error, "Unable to find SteamCMD in provided path, please check the SteamCMD path in settings.json");
				Helpers.ExitWithError();
			}
		}

		public void Run()
		{
			// Get game information
			Log.ConsolePrint(LogLevel.Info, "Fetching public build number for Atlas from Steam3");
			int BuildNumber = SteamInt.GetGameInformation(1006030);
			if( BuildNumber == -1 )
			{
				Log.ConsolePrint(LogLevel.Error, "Unable to fetch Build ID from Steam, this may be an issue with your internet connection.");
			}
			else
			{
				Log.ConsolePrint(LogLevel.Success, "Current Build ID for Atlas is: {0}", BuildNumber);
			}

			// Initial Setup
            //we only need to check for an update on one server - Atlas only has one map with multiple grids 

			Log.ConsolePrint(LogLevel.Debug, "Initializing Atlas Server");
            Log.ConsolePrint(LogLevel.Debug, "There are '{0}' Grids", numOfGrids);
            //int CurrentAppID = SteamInt.GetGameBuildVersion(Servers[0].ServerData.GameServerPath);
            int CurrentAppID = SteamInt.GetGameBuildVersion(this.ATLASConfiguration.GameServerPath);

            Servers[0].LastBackedUp = Helpers.CurrentUnixStamp;
			if( (CurrentAppID < BuildNumber) && (CurrentAppID != -1) )
			{
				// Update Available - make sure its not running
				if( !ServerInt.ServerRunning(Servers[0].ServerData) )
				{
					// Update Server
					Log.ConsolePrint(LogLevel.Info, "The Atlas Server has an update available, Updating before we start the server up.");
					//SteamInt.UpdateGame(Servers[0].ServerData.SteamUpdateScript, ATLASConfiguration.ShowSteamUpdateInConsole);
                    SteamInt.UpdateGame(ATLASConfiguration.SteamUpdateScript, ATLASConfiguration.ShowSteamUpdateInConsole);
                    Log.ConsolePrint(LogLevel.Success, "Atlas Server update successful, starting server.");
				}
                else
                {
                    Log.ConsolePrint(LogLevel.Error, "The Atlas Server has an update available, But the server is running");
                }

                //var ProcessID = ServerInt.StartServer(Servers[0].ServerData);
                //Servers[0].ProcessID = ProcessID;
            }

            //loop through and start each server and save each PID
            foreach( var Server in Servers )
            {
            // Start Server
                Log.ConsolePrint(LogLevel.Info, "Atlas Server is up to date. Starting/Connecting to server shard '{0}'", Server.ServerData.GameServerName);
                //start each shard and grab the PID
			    var ProcessID = ServerInt.StartServer(Server.ServerData);
			    Server.ProcessID = ProcessID;
			}

			// Application Loop
			int LastUpdatePollTime = Helpers.CurrentUnixStamp;
			int LastMinutePollTime = Helpers.CurrentUnixStamp;

			int PreviousBuild = BuildNumber;
			bool UpdatesQueued = false;

			while( _Running )
			{
                //check for an update every x minutes - x is in the settings.json file
				if( (LastUpdatePollTime + (60 * ATLASConfiguration.UpdatePollingInMinutes) < Helpers.CurrentUnixStamp) && !UpdatesQueued )
				{
					// Query Steam and check for updates to our servers
					Log.ConsolePrint(LogLevel.Debug, "Checking with Steam for updates to Atlas (Current Build: {0})", BuildNumber);
					BuildNumber = SteamInt.GetGameInformation(1006030);

					if( BuildNumber != -1 )
					{
						LastUpdatePollTime = Helpers.CurrentUnixStamp;
						if( ( BuildNumber > PreviousBuild ) && ( PreviousBuild != -1 ) ) Log.ConsolePrint(LogLevel.Info, "A new build of Atlas is available. Build number: {0}", BuildNumber);
					}


                    //Query each server during update check for players online and print to console and write to log
                    foreach (var Server in Servers)
                    {

                        using (var Query = new SrcQuery("127.0.0.1", Server.ServerData.QueryPort))
                        {
                            try
                            {
                                var QueryData = Query.QueryServer();
                                    Log.ConsolePrint(LogLevel.Info, "Server '{0}' has {1} Players online", Server.ServerData.GameServerName, QueryData["CurrentPlayers"]);
                                    continue;
                            }
                            catch (QueryException) { }
                        }
                    }

                }

				bool MinutePassed = ( LastMinutePollTime + 60 <= Helpers.CurrentUnixStamp ) ? true : false;
				//foreach( var Server in Servers )
				//{
					if( Servers[0].MinutesRemaining == -1 )
					{
						// Check each server for updates
						var ServerBuild = SteamInt.GetGameBuildVersion(ATLASConfiguration.GameServerPath);
                        if ((ServerBuild < BuildNumber) && (ServerBuild != -1))
                        { // we have an update available

                            foreach( var Server in Servers )
                            {
                                
                                // Schedule update on server with the user defined interval.
                                Log.ConsolePrint(LogLevel.Success, "Server '{0}' queued for update successfully. Update will begin in {1} minute(s)", Server.ServerData.GameServerName, ATLASConfiguration.UpdateWarningTimeInMinutes);
                                Server.MinutesRemaining = ATLASConfiguration.UpdateWarningTimeInMinutes;
                                if (!UpdatesQueued) UpdatesQueued = true;

                                // Send warning message to All Grid Servers
                                using (var RCONClient = new ATLASRCON("127.0.0.1", Server.ServerData.RCONPort))
                                {
                                    try
                                    {
                                        RCONClient.Authenticate(Server.ServerData.ServerAdminPassword);
                                        string RCONWarning = string.Format(ATLASConfiguration.Messages.ServerUpdateBroadcast, Server.MinutesRemaining);
                                        RCONClient.ExecuteCommand(string.Format("serverchat {0}", RCONWarning));
                                    }
                                    catch (QueryException) { }
                                }
                            }
					    }
					}

					if( MinutePassed && (Servers[0].MinutesRemaining >= 1) )
					{
						Log.ConsolePrint(LogLevel.Debug, "Ticking update minute counter from {0} to {1} for server '{2}'", Servers[0].MinutesRemaining, Servers[0].MinutesRemaining-1, "Atlas Server");
                        foreach (var Server in Servers)
                        {
                            Server.MinutesRemaining = (Server.MinutesRemaining - 1);
                        }
                        // Send warning message to each Server Shard
                        if (Servers[0].MinutesRemaining != 0)
                        {
                            foreach (var Server in Servers)
                            {
                                using (var RCONClient = new ATLASRCON("127.0.0.1", Server.ServerData.RCONPort))
                                {
                                    try
                                    {
                                        RCONClient.Authenticate(Server.ServerData.ServerAdminPassword);
                                        string RCONWarning = string.Format(ATLASConfiguration.Messages.ServerUpdateBroadcast, Server.MinutesRemaining);
                                        RCONClient.ExecuteCommand(string.Format("serverchat {0}", RCONWarning));
                                    }
                                    catch (QueryException Ex)
                                    {
                                        Log.ConsolePrint(LogLevel.Error, Ex.Message);
                                    }
                                }
                            }
                        }
					}
                    //Warnings are over time to shutdown and update
					if( Servers[0].MinutesRemaining == 0 )
					{
                        foreach (var Server in Servers)
                        {
                            // Send warning message to Server
                            using (var RCONClient = new ATLASRCON("127.0.0.1", Server.ServerData.RCONPort))
                            {
                                try
                                {
                                    RCONClient.Authenticate(Server.ServerData.ServerAdminPassword);
                                    RCONClient.ExecuteCommand(string.Format("serverchat {0}", ATLASConfiguration.Messages.ServerShutdownBroadcast));
                                }
                                catch (QueryException Ex)
                                {
                                    Log.ConsolePrint(LogLevel.Error, Ex.Message);
                                }
                            }

                            _Sleeper.WaitOne(TimeSpan.FromSeconds(2));

                            // Shutdown Server
                            var ResetEvent = new AutoResetEvent(false);
                            ServerInt.StopServer(Server.ServerData, ResetEvent);
                            Log.ConsolePrint(LogLevel.Info, "Server shard '{0}' will now be shutdown for an update", Server.ServerData.GameServerName);

                            ResetEvent.WaitOne();
                            Log.ConsolePrint(LogLevel.Debug, "Server shard '{0}' now waiting for process exit", Server.ServerData.GameServerName);
                        }

						// Update Server - only need to update one
						SteamInt.UpdateGame(ATLASConfiguration.SteamUpdateScript, ATLASConfiguration.ShowSteamUpdateInConsole);
						_Sleeper.WaitOne( TimeSpan.FromSeconds(4) );

                        //Update is done - time to restart each server shard
                        foreach (var Server in Servers)
                        {
                            // Restart All Grids 
                            Log.ConsolePrint(LogLevel.Info, "Server '{0}' update complete, restarting server.", Server.ServerData.GameServerName);
                            var ProcessID = ServerInt.StartServer(Server.ServerData);

                            Server.MinutesRemaining = -1;
                            Server.ProcessID = ProcessID;
                        }

					}
                    //Backup stuff
					if( ATLASConfiguration.Backup.EnableBackup )
					{
                        foreach (var Server in Servers)
                        {

                            // Check for Last Backup
                            if ((Server.LastBackedUp + (60 * ATLASConfiguration.Backup.BackupIntervalInMinutes) < Helpers.CurrentUnixStamp) && !UpdatesQueued)
                            {
                                Server.LastBackedUp = Helpers.CurrentUnixStamp;
                                BackupInt.BackupServer(Server.ServerData);
                                BackupInt.CleanBackups(Server.ServerData);
                            }
                        }
					}

				// Cleanup before next loop
				int NumberServersUpdateRemaining = Servers.Where(x => x.MinutesRemaining != -1).Count();
				if( NumberServersUpdateRemaining <= 0 && UpdatesQueued ) UpdatesQueued = false;

				PreviousBuild = BuildNumber;
				if( MinutePassed ) LastMinutePollTime = Helpers.CurrentUnixStamp;
				_Sleeper.WaitOne( TimeSpan.FromSeconds(5) );
			}
		}

		public void Shutdown()
		{
			// Shutdown Main Thread
			Log.ConsolePrint(LogLevel.Info, "Shutting down");
			_Running = false;

			_Sleeper.Set();
			Thread.Sleep(500);
		} 
	}
}