﻿using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class SatIpHost : M3UTunerHost, ITunerHost
    {
       
        public SatIpHost(IConfigurationManager config, ILogger logger, IJsonSerializer jsonSerializer, IMediaEncoder mediaEncoder, IFileSystem fileSystem, IHttpClient httpClient)
            : base(config, logger, jsonSerializer, mediaEncoder,fileSystem,httpClient)
        {

        }
          
        protected override async Task<IEnumerable<ChannelInfo>> GetChannelsInternal(TunerHostInfo tuner, CancellationToken cancellationToken)
        {
            var satInfo = (SatIpTunerHostInfo) tuner;

            return await new M3uParser(Logger, _fileSystem, _httpClient).Parse(satInfo.M3UUrl, tuner.Id, cancellationToken).ConfigureAwait(false);
        }

        public static string DeviceType
        {
            get { return "satip"; }
        }

        public override string Type
        {
            get { return DeviceType; }
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
            .SelectMany(i => GetTunerInfos(i, cancellationToken))
            .ToList();

            return Task.FromResult(list);
        }

        public List<LiveTvTunerInfo> GetTunerInfos(TunerHostInfo info, CancellationToken cancellationToken)
        {
            var satInfo = (SatIpTunerHostInfo) info;

            var list = new List<LiveTvTunerInfo>();

            for (var i = 0; i < satInfo.Tuners; i++)
            {
                list.Add(new LiveTvTunerInfo
                {
                    Name = satInfo.FriendlyName ?? Name,
                    SourceType = Type,
                    Status = LiveTvTunerStatus.Available,
                    Id = info.Url.GetMD5().ToString("N") + i.ToString(CultureInfo.InvariantCulture),
                    Url = info.Url
                });
            }

            return list;
        }
    }
}
