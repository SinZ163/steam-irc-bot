﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net;
using SteamKit2;
using System.Runtime.ExceptionServices;

namespace SteamIrcBot
{
    class UpToDateJob : Job
    {
        ConcurrentDictionary<uint, uint> versionMap;


        public UpToDateJob( CallbackManager manager )
        {
            Period = TimeSpan.FromMinutes( 2 );

            versionMap = new ConcurrentDictionary<uint, uint>();
        }

        protected override void OnRun()
        {
            Parallel.ForEach( Settings.Current.ImportantApps, app =>
            {
                uint lastVersion = 0;
                bool hasLastVersion = versionMap.TryGetValue( app.AppID, out lastVersion );

                using ( dynamic steamApps = WebAPI.GetInterface( "ISteamApps" ) )
                {
                    steamApps.Timeout = TimeSpan.FromSeconds(30);

                    KeyValue results = null;

                    try
                    {
                        results = steamApps.UpToDateCheck( appid: app.AppID, version: lastVersion );
                    }
                    catch ( WebException ex )
                    {
                        if ( ex.Status != WebExceptionStatus.Timeout )
                        {
                            // log everything but timeouts
                            Log.WriteWarn( "UpToDateJob", "Unable to make UpToDateCheck request: {0}", ex.Message );
                        }

                        return;
                    }
                    catch ( TimeoutException )
                    {
                        // no need to log timeouts
                        return;
                    }

                    if ( !results[ "success" ].AsBoolean() )
                        return; // no useful result from the api, or app isn't configured

                    uint requiredVersion = ( uint )results[ "required_version" ].AsInteger( -1 );

                    if ( ( int )requiredVersion == -1 )
                        return; // some apps are incorrectly configured and don't report a required version

                    if ( !results[ "up_to_date" ].AsBoolean() )
                    {
                        // update our cache of required version
                        versionMap[ app.AppID ] = requiredVersion;

                        if ( hasLastVersion )
                        {
                            // if we previously cached the version, display that it changed
                            IRC.Instance.SendToTag( "game-updates", "{0} (version: {1}) is no longer up to date. New version: {2}", Steam.Instance.GetAppName( app.AppID ), lastVersion, requiredVersion );
                        }
                    }

                }
            } );
        }
    }
}
