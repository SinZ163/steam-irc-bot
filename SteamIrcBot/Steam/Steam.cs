﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.IO;
using SteamKit2;
using SteamKit2.Unified.Internal;
using SteamKit2.Internal;

namespace SteamIrcBot
{
    class Steam
    {
        static Steam _instance = new Steam();
        public static Steam Instance { get { return _instance; } }


        public SteamClient Client { get; private set; }

        public uint CellID { get; private set; }

        public CallbackManager CallbackManager { get; private set; }

        public GCManager GCManager { get; private set; }
        public SteamManager SteamManager { get; private set; }

        public JobManager JobManager { get; private set; }

        public SteamUser User { get; private set; }
        public SteamFriends Friends { get; private set; }
        public SteamApps Apps { get; private set; }
        public SteamUserStats UserStats { get; private set; }
        public SteamCloud Cloud { get; private set; }
        public SteamWorkshop Workshop { get; private set; }
        public SteamUnifiedMessages Unified { get; private set; }
        public SteamMasterServer MasterServer { get; set; }
        public SteamGameCoordinator GameCoordinator { get; private set; }
        public SteamLevels Levels { get; private set; }
        public SteamGames Games { get; private set; }
        public SteamAppInfo AppInfo { get; private set; }
        public SteamAccount Account { get; private set; }

        public SteamUnifiedMessages.UnifiedService<IPublishedFile> PublishedFiles { get; private set; }
        public SteamUnifiedMessages.UnifiedService<ICommunity> Community { get; private set; }


        bool loggedOn;
        public bool Connected { get { return loggedOn; } }

        bool shuttingDown = false;

        DateTime nextConnect = DateTime.MaxValue;
        bool nextConnectDelayed = false;


        public void Init()
        {
            Client = new SteamClient();

            CallbackManager = new CallbackManager( Client );

            User = Client.GetHandler<SteamUser>();
            Friends = Client.GetHandler<SteamFriends>();
            Apps = Client.GetHandler<SteamApps>();
            UserStats = Client.GetHandler<SteamUserStats>();
            Cloud = Client.GetHandler<SteamCloud>();
            Workshop = Client.GetHandler<SteamWorkshop>();
            Unified = Client.GetHandler<SteamUnifiedMessages>();
            MasterServer = Client.GetHandler<SteamMasterServer>();
            GameCoordinator = Client.GetHandler<SteamGameCoordinator>();
            Levels = new SteamLevels();
            Games = new SteamGames();
            AppInfo = new SteamAppInfo();
            Account = new SteamAccount();

            PublishedFiles = Unified.CreateService<IPublishedFile>();
            Community = Unified.CreateService<ICommunity>();

            Client.AddHandler( Levels );
            Client.AddHandler( Games );
            Client.AddHandler( AppInfo );
            Client.AddHandler( Account );

            SteamManager = new SteamManager( CallbackManager );
            GCManager = new GCManager( CallbackManager );

            JobManager = new JobManager( CallbackManager );

            CallbackManager.Subscribe<SteamClient.ConnectedCallback>( OnConnected );
            CallbackManager.Subscribe<SteamClient.DisconnectedCallback>( OnDisconnected );

            CallbackManager.Subscribe<SteamUser.LoggedOnCallback>( OnLoggedOn );
            CallbackManager.Subscribe<SteamUser.LoggedOffCallback>( OnLoggedOff );

            CallbackManager.Subscribe<SteamUser.AccountInfoCallback>( OnAccountInfo );
        }


        public void Connect()
        {
            JobManager.Start();
            nextConnect = DateTime.Now;
        }

        public void Disconnect()
        {
            if ( Client == null )
            {
                // disconnecting before we've even initialized
                // such as when settings validation fails
                return;
            }

            Log.WriteInfo( "Steam", "Disconnecting..." );

            shuttingDown = true;

            Client.Disconnect();

            JobManager.Stop();
        }


        public string GetAppName( uint appId )
        {
            string appName;

            if ( !AppInfo.GetAppName( appId, out appName ) )
                return appId.ToString();

            return string.Format( "{0} ({1})", appName, appId );
        }

        public string GetPackageName( uint packageId )
        {
            string packageName;

            if ( !AppInfo.GetPackageName( packageId, out packageName ) )
                return packageId.ToString();

            return string.Format( "{0} ({1})", packageName, packageId );
        }

        public string GetDepotName( uint depotId, uint appId )
        {
            string depotName;

            if ( !AppInfo.GetDepotName( depotId, appId, out depotName ) )
                return depotId.ToString();

            return string.Format( "{0} ({1})", depotName, depotId );
        }


        public void Tick()
        {
            CallbackManager.RunWaitCallbacks( TimeSpan.FromMilliseconds( 200 ) );

            SteamManager.Tick();

            if ( DateTime.Now >= nextConnect )
            {
                nextConnect = DateTime.MaxValue;

                Log.WriteInfo( "Steam", "Connecting to Steam..." );

                IRC.Instance.SendEmoteToTag( "steam-logon", "Connecting to Steam..." );
                Client.Connect();
            }
        }


        void Reconnect( TimeSpan when )
        {
            nextConnect = DateTime.Now + when;

            if ( nextConnectDelayed )
            {
                nextConnect += TimeSpan.FromSeconds( 20 );
                nextConnectDelayed = false;
            }
        }


        void OnConnected( SteamClient.ConnectedCallback callback )
        {
            loggedOn = false;

            Log.WriteInfo( "Steam", "Connected to Steam3 " );

            IRC.Instance.SendEmoteToTag( "steam-logon", "Connected to Steam! Logging on..." );

            User.LogOn( new SteamUser.LogOnDetails
            {
                Username = Settings.Current.SteamUsername,
                Password = Settings.Current.SteamPassword
            } );
        }
        void OnDisconnected( SteamClient.DisconnectedCallback callback )
        {
            loggedOn = false;

            if ( shuttingDown )
            {
                Log.WriteInfo( "Steam", "Disconnected due to service shutdown" );
                return;
            }

            Log.WriteInfo( "Steam", "Disconnected from Steam" );
            
            IRC.Instance.SendEmoteToTag( "steam-logon", $"Disconnected from Steam! Reconnecting in {(nextConnectDelayed ? "30" : "10")}..." );

            Reconnect( TimeSpan.FromSeconds( 10 ) );
        }

        void OnLoggedOn( SteamUser.LoggedOnCallback callback )
        {
            if ( callback.Result != EResult.OK )
            {
                if ( callback.Result == EResult.RateLimitExceeded || callback.Result == EResult.TryAnotherCM )
                {
                    // fingers crossed
                    nextConnectDelayed = true;
                }

                Log.WriteWarn( "Steam", "Unable to logon to Steam3: {0} / {1}", callback.Result, callback.ExtendedResult );

                IRC.Instance.SendEmoteToTag( "steam-logon", "Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult );
                return;
            }

            loggedOn = true;
            nextConnectDelayed = false;

            CellID = callback.CellID;

            Log.WriteInfo( "Steam", "Logged onto Steam3!" );

            IRC.Instance.SendEmoteToTag( "steam", "Logged on to Steam! Server time: {0}", callback.ServerTime );
            ClientMsgProtobuf<CMsgClientUIMode> request = new ClientMsgProtobuf<CMsgClientUIMode>(EMsg.ClientCurrentUIMode) { Body = { chat_mode = 2 } };
            Client.Send(request);
            JobManager.ForceRun<GameSessionJob>();
        }

        void OnLoggedOff( SteamUser.LoggedOffCallback callback )
        {
            Log.WriteWarn( "Steam", "Logged off Steam3: {0}", callback.Result );

            IRC.Instance.SendEmoteToTag( "steam", "Logged off of Steam: {0}", callback.Result );
        }

        void OnAccountInfo( SteamUser.AccountInfoCallback callback )
        {
            // go online on friends in order to receive clan state callbacks
            Friends.SetPersonaState( EPersonaState.Online );
        }
    }
}
