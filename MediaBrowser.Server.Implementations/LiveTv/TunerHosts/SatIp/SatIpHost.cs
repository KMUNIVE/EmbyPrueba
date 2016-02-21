﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonIO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Server.Implementations.LiveTv.TunerHosts.SatIp
{
    public class SatIpHost : BaseTunerHost, ITunerHost
    {
        private readonly IFileSystem _fileSystem;
        private readonly IHttpClient _httpClient;
        
        public SatIpHost(IConfigurationManager config, ILogger logger, IJsonSerializer jsonSerializer, IMediaEncoder mediaEncoder, IFileSystem fileSystem, IHttpClient httpClient)
            : base(config, logger, jsonSerializer, mediaEncoder)
        {
            _fileSystem = fileSystem;
            _httpClient = httpClient;
        }
        
        protected override async Task<IEnumerable<ChannelInfo>> GetChannelsInternal(TunerHostInfo tuner, CancellationToken cancellationToken)
        {
            var satInfo = (SatIpTunerHostInfo) tuner;

            return await new M3uParser(Logger, _fileSystem, _httpClient).Parse(satInfo.M3UUrl, cancellationToken).ConfigureAwait(false);
        }

        public static string DeviceType
        {
            get { return "satip"; }
        }

        public override string Type
        {
            get { return DeviceType; }
        }

        protected override async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(TunerHostInfo tuner, string path, CancellationToken cancellationToken)
        {

                MediaProtocol protocol = MediaProtocol.File;
                if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    protocol = MediaProtocol.Http;
                }
                else if (path.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase))
                {
                    protocol = MediaProtocol.Rtmp;
                }
                else if (path.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase))
                {
                    protocol = MediaProtocol.Rtsp;
                }

                var mediaSource = new MediaSourceInfo
                {
                    Path = path,
                    Protocol = protocol,
                    MediaStreams = new List<MediaStream>
                    {
                        new MediaStream
                        {
                            Type = MediaStreamType.Video,
                            // Set the index to -1 because we don't know the exact index of the video stream within the container
                            Index = -1,
                            IsInterlaced = true
                        },
                        new MediaStream
                        {
                            Type = MediaStreamType.Audio,
                            // Set the index to -1 because we don't know the exact index of the audio stream within the container
                            Index = -1

                        }
                    },
                    RequiresOpening = false,
                    RequiresClosing = false
                };

                return new List<MediaSourceInfo> { mediaSource };

        }

        protected override async Task<MediaSourceInfo> GetChannelStream(TunerHostInfo tuner, string channelId, string streamId, CancellationToken cancellationToken)
        {
            var sources = await GetChannelStreamMediaSources(tuner, channelId, cancellationToken).ConfigureAwait(false);

            return sources.First();
        }

        protected override async Task<bool> IsAvailableInternal(TunerHostInfo tuner, string channelId, CancellationToken cancellationToken)
        {
            var updatedInfo = await SatIpDiscovery.Current.GetInfo(tuner.Url, cancellationToken).ConfigureAwait(false);

            return updatedInfo.TunersAvailable > 0;
        }

        protected override List<TunerHostInfo> GetTunerHosts()
        {
            return SatIpDiscovery.Current.DiscoveredHosts;
        }

        public string Name
        {
            get { return "Sat IP"; }
        }

        public Task<List<LiveTvTunerInfo>> GetTunerInfos(CancellationToken cancellationToken)
        {
            var list = GetTunerHosts()
            .Select(i => new LiveTvTunerInfo()
            {
                Name = Name,
                SourceType = Type,
                Status = LiveTvTunerStatus.Available,
                Id = i.Url.GetMD5().ToString("N"),
                Url = i.Url
            })
            .ToList();

            return Task.FromResult(list);
        }
    }
}
