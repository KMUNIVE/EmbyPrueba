﻿using MediaBrowser.Common.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using MediaBrowser.Model.Logging;

namespace MediaBrowser.Dlna.PlayTo
{
    public sealed class Device : IDisposable
    {
        const string ServiceAvtransportId = "urn:upnp-org:serviceId:AVTransport";
        const string ServiceRenderingId = "urn:upnp-org:serviceId:RenderingControl";

        #region Fields & Properties

        private Timer _dt;

        public DeviceProperties Properties { get; set; }

        private int _muteVol;
        public bool IsMuted
        {
            get
            {
                return _muteVol > 0;
            }
        }

        string _currentId = String.Empty;
        public string CurrentId
        {
            get
            {
                return _currentId;
            }
            set
            {
                if (_currentId == value)
                    return;
                _currentId = value;
                NotifyCurrentIdChanged(value);
            }
        }

        public int Volume { get; set; }

        public TimeSpan Duration { get; set; }

        private TimeSpan _position = TimeSpan.FromSeconds(0);
        public TimeSpan Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        private string _transportState = String.Empty;
        public string TransportState
        {
            get
            {
                return _transportState;
            }
            set
            {
                if (_transportState == value)
                    return;

                _transportState = value;

                if (value == "PLAYING" || value == "STOPPED")
                    NotifyPlaybackChanged(value == "STOPPED");
            }
        }

        public bool IsPlaying
        {
            get
            {
                return TransportState == "PLAYING";
            }
        }

        public bool IsTransitioning
        {
            get
            {
                return (TransportState == "TRANSITIONING");
            }
        }

        public bool IsPaused
        {
            get
            {
                if (TransportState == "PAUSED" || TransportState == "PAUSED_PLAYBACK")
                    return true;
                return false;
            }
        }

        public bool IsStopped
        {
            get
            {
                return (TransportState == "STOPPED");
            }
        }

        public DateTime UpdateTime
        { get; private set; }

        #endregion

        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;

        #region Constructor & Initializer

        public Device(DeviceProperties deviceProperties, IHttpClient httpClient, ILogger logger)
        {
            Properties = deviceProperties;
            _httpClient = httpClient;
            _logger = logger;
        }

        internal void Start()
        {
            UpdateTime = DateTime.UtcNow;
            _dt = new Timer(1000);
            _dt.Elapsed += dt_Elapsed;
            _dt.Start();
        }

        #endregion

        #region Commanding

        public Task<bool> VolumeDown(bool mute = false)
        {
            var sendVolume = (Volume - 5) > 0 ? Volume - 5 : 0;
            if (mute && _muteVol == 0)
            {
                sendVolume = 0;
                _muteVol = Volume;
            }
            return SetVolume(sendVolume);
        }

        public Task<bool> VolumeUp(bool unmute = false)
        {
            var sendVolume = (Volume + 5) < 100 ? Volume + 5 : 100;
            if (unmute && _muteVol > 0)
                sendVolume = _muteVol;
            _muteVol = 0;
            return SetVolume(sendVolume);
        }

        public Task ToggleMute()
        {
            if (_muteVol == 0)
            {
                _muteVol = Volume;
                return SetVolume(0);
            }

            int tmp = _muteVol;
            _muteVol = 0;
            return SetVolume(tmp);

        }

        public async Task<bool> SetVolume(int value)
        {
            var command = RendererCommands.ServiceActions.FirstOrDefault(c => c.Name == "SetVolume");
            if (command == null)
                return true;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceRenderingId);

            if (service == null)
            {
                throw new InvalidOperationException("Unable to find service");
            }

            var result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType, value))
                .ConfigureAwait(false);
            Volume = value;
            return true;
        }

        public async Task<TimeSpan> Seek(TimeSpan value)
        {
            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "Seek");
            if (command == null)
                return value;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);

            if (service == null)
            {
                throw new InvalidOperationException("Unable to find service");
            }

            var result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, AvCommands.BuildPost(command, service.ServiceType, String.Format("{0:hh}:{0:mm}:{0:ss}", value), "REL_TIME"))
                .ConfigureAwait(false);

            return value;
        }

        public async Task<bool> SetAvTransport(string url, string header, string metaData)
        {
            _dt.Stop();
            TransportState = "STOPPED";
            CurrentId = "0";

            await Task.Delay(50).ConfigureAwait(false);

            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "SetAVTransportURI");
            if (command == null)
                return false;

            var dictionary = new Dictionary<string, string>
            {
                {"CurrentURI", url},
                {"CurrentURIMetaData", CreateDidlMeta(metaData)}
            };

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);

            if (service == null)
            {
                throw new InvalidOperationException("Unable to find service");
            }

            var result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, AvCommands.BuildPost(command, service.ServiceType, url, dictionary), header)
                .ConfigureAwait(false);

            if (!IsPlaying)
            {
                await Task.Delay(50).ConfigureAwait(false);
                await SetPlay().ConfigureAwait(false);
            }

            _count = 5;
            _dt.Start();
            return true;
        }

        private string CreateDidlMeta(string value)
        {
            if (value == null)
                return String.Empty;

            var escapedData = value.Replace("<", "&lt;").Replace(">", "&gt;");

            return String.Format(BaseDidl, escapedData.Replace("\r\n", ""));
        }

        private const string BaseDidl = "&lt;DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns:dlna=\"urn:schemas-dlna-org:metadata-1-0/\"&gt;{0}&lt;/DIDL-Lite&gt;";

        public async Task<bool> SetNextAvTransport(string value, string header, string metaData)
        {
            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "SetNextAVTransportURI");
            if (command == null)
                return false;

            var dictionary = new Dictionary<string, string>();
            dictionary.Add("NextURI", value);
            dictionary.Add("NextURIMetaData", CreateDidlMeta(metaData));

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);

            if (service == null)
            {
                throw new InvalidOperationException("Unable to find service");
            }

            var result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, AvCommands.BuildPost(command, service.ServiceType, value, dictionary), header)
                .ConfigureAwait(false);

            await Task.Delay(100).ConfigureAwait(false);

            return true;
        }

        public async Task<bool> SetPlay()
        {
            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "Play");
            if (command == null)
                return false;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);

            if (service == null)
            {
                throw new InvalidOperationException("Unable to find service");
            }

            var result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType, 1))
                .ConfigureAwait(false);

            _count = 5;
            return true;
        }

        public async Task<bool> SetStop()
        {
            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "Stop");
            if (command == null)
                return false;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);

            var result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType, 1))
                .ConfigureAwait(false);

            await Task.Delay(50).ConfigureAwait(false);
            _count = 4;
            return true;
        }

        public async Task<bool> SetPause()
        {
            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "Pause");
            if (command == null)
                return false;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);

            var result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType, 0))
                .ConfigureAwait(false);

            await Task.Delay(50).ConfigureAwait(false);
            TransportState = "PAUSED_PLAYBACK";
            return true;
        }

        #endregion

        #region Get data

        // TODO: What is going on here
        int _count = 5;

        async void dt_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            ((Timer)sender).Stop();
            var hasTrack = await GetPositionInfo().ConfigureAwait(false);

            // TODO: Why make these requests if hasTrack==false?
            if (_count > 4)
            {
                await GetTransportInfo().ConfigureAwait(false);
                if (!hasTrack)
                {
                    await GetMediaInfo().ConfigureAwait(false);
                }
                await GetVolume().ConfigureAwait(false);
                _count = 0;
            }

            _count++;
            if (_disposed)
                return;
            ((Timer)sender).Start();
        }

        private async Task GetVolume()
        {
            var command = RendererCommands.ServiceActions.FirstOrDefault(c => c.Name == "GetVolume");
            if (command == null)
                return;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceRenderingId);

            if (service == null)
            {
                throw new InvalidOperationException("Unable to find service");
            }

            XDocument result;

            try
            {
                result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType))
                   .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting volume info", ex);
                return;
            }

            if (result == null || result.Document == null)
                return;

            var volume = result.Document.Descendants(uPnpNamespaces.RenderingControl + "GetVolumeResponse").Select(i => i.Element("CurrentVolume")).FirstOrDefault(i => i != null);
            var volumeValue = volume == null ? null : volume.Value;

            if (volumeValue == null)
                return;

            Volume = Int32.Parse(volumeValue);

            //Reset the Mute value if Volume is bigger than zero
            if (Volume > 0 && _muteVol > 0)
            {
                _muteVol = 0;
            }
        }

        private async Task GetTransportInfo()
        {
            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "GetTransportInfo");
            if (command == null)
                return;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);
            if (service == null)
                return;

            XDocument result;

            try
            {
                result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType))
                   .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting transport info", ex);
                return;
            }

            if (result == null || result.Document == null)
                return;

            var transportState =
                result.Document.Descendants(uPnpNamespaces.AvTransport + "GetTransportInfoResponse").Select(i => i.Element("CurrentTransportState")).FirstOrDefault(i => i != null);

            var transportStateValue = transportState == null ? null : transportState.Value;

            if (transportStateValue != null)
                TransportState = transportStateValue;

            UpdateTime = DateTime.UtcNow;
        }

        private async Task GetMediaInfo()
        {
            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "GetMediaInfo");
            if (command == null)
                return;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);

            if (service == null)
            {
                throw new InvalidOperationException("Unable to find service");
            }

            XDocument result;

            try
            {
                result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType))
                   .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting media info", ex);
                return;
            }

            if (result == null || result.Document == null)
                return;

            var track = result.Document.Descendants("CurrentURIMetaData").Select(i => i.Value).FirstOrDefault();

            if (String.IsNullOrEmpty(track))
            {
                CurrentId = "0";
                return;
            }

            var uPnpResponse = XElement.Parse(track);

            var e = uPnpResponse.Element(uPnpNamespaces.items) ?? uPnpResponse;

            var uTrack = uParser.CreateObjectFromXML(new uParserObject
            {
                Type = e.GetValue(uPnpNamespaces.uClass),
                Element = e
            });

            if (uTrack != null)
                CurrentId = uTrack.Id;
        }

        private async Task<bool> GetPositionInfo()
        {
            var command = AvCommands.ServiceActions.FirstOrDefault(c => c.Name == "GetPositionInfo");
            if (command == null)
                return true;

            var service = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);

            if (service == null)
            {
                throw new InvalidOperationException("Unable to find service");
            }

            XDocument result;

            try
            {
                result = await new SsdpHttpClient(_httpClient).SendCommandAsync(Properties.BaseUrl, service, command.Name, RendererCommands.BuildPost(command, service.ServiceType))
                   .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting position info", ex);
                return false;
            }

            if (result == null || result.Document == null)
                return true;

            var durationElem = result.Document.Descendants(uPnpNamespaces.AvTransport + "GetPositionInfoResponse").Select(i => i.Element("TrackDuration")).FirstOrDefault(i => i != null);
            var duration = durationElem == null ? null : durationElem.Value;

            if (duration != null)
            {
                Duration = TimeSpan.Parse(duration);
            }

            var positionElem = result.Document.Descendants(uPnpNamespaces.AvTransport + "GetPositionInfoResponse").Select(i => i.Element("RelTime")).FirstOrDefault(i => i != null);
            var position = positionElem == null ? null : positionElem.Value;

            if (position != null)
            {
                Position = TimeSpan.Parse(position);
            }

            var track = result.Document.Descendants("TrackMetaData").Select(i => i.Value)
                .FirstOrDefault();

            if (String.IsNullOrEmpty(track))
            {
                //If track is null, some vendors do this, use GetMediaInfo instead                    
                return false;
            }

            var uPnpResponse = XElement.Parse(track);

            var e = uPnpResponse.Element(uPnpNamespaces.items) ?? uPnpResponse;

            var uTrack = uBaseObject.Create(e);

            if (uTrack == null)
                return true;

            CurrentId = uTrack.Id;

            return true;
        }

        #endregion

        #region From XML

        private async Task GetAVProtocolAsync()
        {
            var avService = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceAvtransportId);
            if (avService == null)
                return;

            var url = avService.SCPDURL;
            if (!url.Contains("/"))
                url = "/dmr/" + url;
            if (!url.StartsWith("/"))
                url = "/" + url;

            var httpClient = new SsdpHttpClient(_httpClient);
            var document = await httpClient.GetDataAsync(new Uri(Properties.BaseUrl + url));

            AvCommands = TransportCommands.Create(document);
        }

        private async Task GetRenderingProtocolAsync()
        {
            var avService = Properties.Services.FirstOrDefault(s => s.ServiceId == ServiceRenderingId);

            if (avService == null)
                return;
            string url = avService.SCPDURL;
            if (!url.Contains("/"))
                url = "/dmr/" + url;
            if (!url.StartsWith("/"))
                url = "/" + url;

            var httpClient = new SsdpHttpClient(_httpClient);
            var document = await httpClient.GetDataAsync(new Uri(Properties.BaseUrl + url));

            RendererCommands = TransportCommands.Create(document);
        }

        internal TransportCommands AvCommands
        {
            get;
            set;
        }

        internal TransportCommands RendererCommands
        {
            get;
            set;
        }

        public static async Task<Device> CreateuPnpDeviceAsync(Uri url, IHttpClient httpClient, ILogger logger)
        {
            var ssdpHttpClient = new SsdpHttpClient(httpClient);

            var document = await ssdpHttpClient.GetDataAsync(url).ConfigureAwait(false);

            var deviceProperties = new DeviceProperties();

            var name = document.Descendants(uPnpNamespaces.ud.GetName("friendlyName")).FirstOrDefault();
            if (name != null)
                deviceProperties.Name = name.Value;

            var name2 = document.Descendants(uPnpNamespaces.ud.GetName("roomName")).FirstOrDefault();
            if (name2 != null)
                deviceProperties.Name = name2.Value;

            var model = document.Descendants(uPnpNamespaces.ud.GetName("modelName")).FirstOrDefault();
            if (model != null)
                deviceProperties.ModelName = model.Value;

            var modelNumber = document.Descendants(uPnpNamespaces.ud.GetName("modelNumber")).FirstOrDefault();
            if (modelNumber != null)
                deviceProperties.ModelNumber = modelNumber.Value;

            var uuid = document.Descendants(uPnpNamespaces.ud.GetName("UDN")).FirstOrDefault();
            if (uuid != null)
                deviceProperties.UUID = uuid.Value;

            var manufacturer = document.Descendants(uPnpNamespaces.ud.GetName("manufacturer")).FirstOrDefault();
            if (manufacturer != null)
                deviceProperties.Manufacturer = manufacturer.Value;

            var manufacturerUrl = document.Descendants(uPnpNamespaces.ud.GetName("manufacturerURL")).FirstOrDefault();
            if (manufacturerUrl != null)
                deviceProperties.ManufacturerUrl = manufacturerUrl.Value;

            var presentationUrl = document.Descendants(uPnpNamespaces.ud.GetName("presentationURL")).FirstOrDefault();
            if (presentationUrl != null)
                deviceProperties.PresentationUrl = presentationUrl.Value;


            deviceProperties.BaseUrl = String.Format("http://{0}:{1}", url.Host, url.Port);

            var icon = document.Descendants(uPnpNamespaces.ud.GetName("icon")).FirstOrDefault();

            if (icon != null)
            {
                deviceProperties.Icon = uIcon.Create(icon);
            }

            var isRenderer = false;

            foreach (var services in document.Descendants(uPnpNamespaces.ud.GetName("serviceList")))
            {
                if (services == null)
                    return null;

                var servicesList = services.Descendants(uPnpNamespaces.ud.GetName("service"));

                if (servicesList == null)
                    return null;

                foreach (var element in servicesList)
                {
                    var service = uService.Create(element);

                    if (service != null)
                    {
                        deviceProperties.Services.Add(service);
                        if (service.ServiceId == ServiceAvtransportId)
                        {
                            isRenderer = true;
                        }
                    }
                }
            }

            if (isRenderer)
            {

                var device = new Device(deviceProperties, httpClient, logger);

                await device.GetRenderingProtocolAsync().ConfigureAwait(false);
                await device.GetAVProtocolAsync().ConfigureAwait(false);

                return device;
            }

            return null;
        }

        #endregion

        #region Events

        public event EventHandler<TransportStateEventArgs> PlaybackChanged;
        public event EventHandler<CurrentIdEventArgs> CurrentIdChanged;

        private void NotifyPlaybackChanged(bool value)
        {
            if (PlaybackChanged != null)
            {
                PlaybackChanged.Invoke(this, new TransportStateEventArgs
                {
                    Stopped = IsStopped
                });
            }
        }

        private void NotifyCurrentIdChanged(string value)
        {
            if (CurrentIdChanged != null)
                CurrentIdChanged.Invoke(this, new CurrentIdEventArgs(value));
        }

        #endregion

        #region IDisposable

        bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _dt.Stop();
            }
        }

        #endregion

        public override string ToString()
        {
            return String.Format("{0} - {1}", Properties.Name, Properties.BaseUrl);
        }
    }
}
