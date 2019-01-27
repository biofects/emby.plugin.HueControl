using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emby.HueControl.Configuration;
//using System.Runtime.Serialization.Json;
using System.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Net;
using System.Net.Http;
using MediaBrowser.Controller.Notifications;
using System.Threading;
using MediaBrowser.Controller.Entities;


namespace Emby.HueControl
{
    public class HueControl : IServerEntryPoint
    {
        private readonly ISessionManager _sessionManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpClient _httpClient;

        public enum PlayerState {Playing, Stopped, Paused, New}

        private List<DeviceState> deviceState = new List<DeviceState>();

        public class DeviceState
        {
            public string deviceId { get; set; }
            public PlayerState playerState;           
        }
        public DeviceState getDeviceState( string deviceId )
        {
            var dState = deviceState.Where(x => x.deviceId == deviceId).FirstOrDefault();
            if (dState == null)
            {
                dState = new DeviceState() { deviceId = deviceId, playerState = PlayerState.New };
                deviceState.Add(dState);
            }
            return dState;
        }

        private List<PauseControl> pauseControl = new List<PauseControl>();
        public class PauseControl
        {
            public string deviceId { get; set; }

            public bool wasPaused { get; set; }
        }

        public PauseControl getPauseControl(string deviceId)
        {
            var c = pauseControl.Where(x => x.deviceId == deviceId).FirstOrDefault();
            if (c == null)
            {
                c = new PauseControl() { deviceId = deviceId };
                pauseControl.Add(c);
            }
            return c;
        }

        public static HueControl Instance { get; private set; }

        public string Name
        {
            get { return "HueControl"; }
        }

        public HueControl(ISessionManager sessionManager, IJsonSerializer jsonSerializer, IHttpClient httpClient, ILogManager logManager, IUserDataManager userDataManager, ILibraryManager libraryManager)
        {
            _logger = logManager.GetLogger(Plugin.Instance.Name);
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
            _userDataManager = userDataManager;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;

            Instance = this;
        }

        public void Dispose()
        {
            //Unbind events
            _sessionManager.PlaybackStart -= PlaybackStart;
            _sessionManager.PlaybackStopped -= PlaybackStopped;
            _sessionManager.PlaybackProgress -= PlaybackProgress;

            //_libraryManager.ItemAdded -= ItemAdded;
        }

        public void Run()
        {
            _sessionManager.PlaybackStart += PlaybackStart;
            _sessionManager.PlaybackStopped += PlaybackStopped;
            _sessionManager.PlaybackProgress += PlaybackProgress;

            //_libraryManager.ItemAdded += ItemAdded;
        }
        
        private void PlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            //we are only interested in video
            if (e.Item.MediaType != "Video") { return; }

            _logger.Debug("e.IsPaused: " + e.IsPaused.ToString());

            //var iType = _libraryManager.GetContentType(e.Item);

            var pauseControl = getPauseControl(e.DeviceId);
            _logger.Debug("pauseControl.wasPaused" + pauseControl.wasPaused.ToString());

            if (e.IsPaused & pauseControl.wasPaused == false)
            {
                _logger.Debug("Playback Paused event");
                _logger.Debug(_jsonSerializer.SerializeToString(e));
                getDeviceState(e.DeviceId).playerState = PlayerState.Paused;
                pauseControl.wasPaused = true;

                var DeviceOptions = Plugin.Instance.Configuration.Options.Where(i => i.embyDeviceID == e.DeviceId && i.Enabled);

                if (DeviceOptions.Count() > 0)
                {
                    var a = DeviceOptions.First();
                    //send dim-up command
                    sDim(DeviceOptions.First());
                }
            }
            else if (e.IsPaused == false & pauseControl.wasPaused)
            {
                _logger.Debug("Playback Resume event");
                _logger.Debug(_jsonSerializer.SerializeToString(e));
                getDeviceState(e.DeviceId).playerState = PlayerState.Playing;
                getPauseControl(e.DeviceId).wasPaused = false;

                var DeviceOptions = Plugin.Instance.Configuration.Options.Where(i => i.embyDeviceID == e.DeviceId && i.Enabled);

                if (DeviceOptions.Count() > 0)
                {
                    var a = DeviceOptions.First();
                    //send dim-down command
                    sDim(DeviceOptions.First());
                }
            }

        }
        private void PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {

            //we are only interested in video
            if (e.Item.MediaType != "Video") { return; }

            _logger.Debug("Playback Start event");
            _logger.Debug(_jsonSerializer.SerializeToString(e));

            getPauseControl(e.DeviceId).wasPaused = false;
            
            getDeviceState(e.DeviceId).playerState = PlayerState.Playing;
            
            var DeviceOptions = Plugin.Instance.Configuration.Options.Where(i => i.embyDeviceID == e.DeviceId && i.Enabled);

            if (DeviceOptions.Count() > 0)
            {
                
                var a = DeviceOptions.First();
                //send dim-down command
                sDim(DeviceOptions.First());
            }
            return;
                        
        }
        private void PlaybackStopped(object sender, PlaybackProgressEventArgs e)
        {
            //we are only interested in video
            if (e.Item.MediaType != "Video") { return; }

            _logger.Debug("Playback Stop event");
            getPauseControl(e.DeviceId).wasPaused = false;

            getDeviceState(e.DeviceId).playerState = PlayerState.Stopped;

            var DeviceOptions = Plugin.Instance.Configuration.Options.Where(i => i.embyDeviceID == e.DeviceId);

            if (DeviceOptions.Count() > 0)
            {
                var a = DeviceOptions.First();
                //send dim-up command
                sDim(DeviceOptions.First());
            }
            return;
        }

        public string hueDataString(DeviceState ds, PluginConfiguration.LightGroup lg){
            string data; 
            var lgOnPlayDim = Math.Floor((decimal)(lg.OnPlay_Dim * 2.54));
            var lgOnStopDim = Math.Floor((decimal)(lg.OnStop_Dim * 2.54));
            var lgOnPauseDim = Math.Floor((decimal)(lg.OnPause_Dim * 2.54));
            switch (ds.playerState)
                {
                    case PlayerState.Playing:
                        if (lg.OnPlay_Dim == 0){
                            data = "{\"on\":false}";
                        } else {
                            data = "{\"on\":true,\"bri\":" + lgOnPlayDim.ToString() + ", \"transitiontime\":" + Math.Floor((decimal)(lg.TransTime / 100)) + "}";
                        }
                        break;
                    case PlayerState.Stopped:
                        if (lg.OnStop_Dim == 0) {
                            data = "{\"on\":false}";
                        } else {
                            data = "{\"on\":true,\"bri\":" + lgOnStopDim.ToString() + ", \"transitiontime\":" + Math.Floor((decimal)(lg.TransTime / 100)) + "}";
                        }
                        break;
                    case PlayerState.Paused:
                        if (lg.OnPause_Dim == 0) {
                            data = "{\"on\":false}";
                        } else {
                            data = "{\"on\":true,\"bri\":" + lgOnPauseDim.ToString() + ", \"transitiontime\":" + Math.Floor((decimal)(lg.TransTime / 100)) + "}";
                        }
                        break;
                    default:
                        data = "New";
                        break;
                }
            return data;
        }
        public string hueUrlString(string lightGroupNumber){
            var bridgOptions = Plugin.Instance.Configuration.bridge;
            return "http://" + bridgOptions.HueIP + "/api/" + bridgOptions.API + "/groups/" + lightGroupNumber + "/action";
        }
        public async void sDim(PluginConfiguration.HueControl o)
        {
            //using transitiontime sets the brightness to 1
            //opening a ticket with Phillips Hue for workaround

            var bridgOptions = Plugin.Instance.Configuration.bridge;
            DeviceState ds = getDeviceState(o.embyDeviceID);
            string data;
            string url;

            foreach (var lGroup in o.LightGroups){
                if(!lGroup.Enabled)
                    continue;
                data = hueDataString(ds, lGroup);
                url = hueUrlString(lGroup.Number);
                try { 
                    await _httpClient.SendAsync(new HttpRequestOptions
                    {
                        Url = url,
                        //Fix for Emby Server 3.6
                        RequestContent = data.AsMemory()
                        //RequestContent = data
                    }, "PUT");
                }
                catch (Exception e)
                {
                    _logger.Debug(e.ToString());
                }
            }
            return;
        }
    }
}