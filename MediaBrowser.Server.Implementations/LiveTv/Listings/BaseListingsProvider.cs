﻿using System.Net;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using static MediaBrowser.Server.Implementations.LiveTv.EmbyTV.EmbyTV;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.LiveTv.Listings
{
    public abstract class BaseListingsProvider
    {
        protected readonly ILogger _logger;
        protected readonly IJsonSerializer _jsonSerializer;

        protected readonly ConcurrentDictionary<string, Station> _stations = new ConcurrentDictionary<string, Station>();

        public BaseListingsProvider(ILogger logger, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
        }

        protected abstract Task<IEnumerable<Station>> GetStations(ListingsProviderInfo info, CancellationToken cancellationToken);

        protected abstract Task<IEnumerable<ProgramInfo>> GetProgramsAsyncInternal(ListingsProviderInfo info, string station, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken);


        public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(ListingsProviderInfo info, ChannelInfo channel, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            IEnumerable<ProgramInfo> programsInfo = new List<ProgramInfo>();

            var station = GetStation(channel);

            if (station == null)
            {
                _logger.Info("No Guide Station found for channel {0} with name {1}", channel.Number, channel.Name);
                return programsInfo;
            }

            programsInfo = await GetProgramsAsyncInternal(info, station.Id, startDateUtc,endDateUtc, cancellationToken);

            foreach (var program in programsInfo)
            {
                program.Id = program.Id+"_"+program.StartDate.ToString("u")+"_"+ channel.Id;
                program.ChannelId = channel.Id;
            }

            return programsInfo;
        }
        private Station GetStation(ChannelInfo channel)
        {
            Station station = null;
            if (_stations.TryGetValue(channel.StationId ?? "", out station)) { return station; }
            return GetStation(channel, _stations.Where(s => s.Key.StartsWith(channel.ListingsProviderId + "_")).Select( s => s.Value));
        }
        private Station GetStation(ChannelInfo channel, IEnumerable<Station> filteredStations)
        {
            Station station = null;
            if (!string.IsNullOrWhiteSpace(channel.Number))
            {
                var result = filteredStations.FirstOrDefault(s => s.ChannelNumbers.Contains(channel.Number));
                if (result != null) { return result; }
            }

            if (!string.IsNullOrWhiteSpace(channel.Name))
            {
                var channelName = NormalizeName(channel.Name);

                var result = filteredStations.FirstOrDefault(i => string.Equals(NormalizeName(i.Callsign ?? string.Empty), channelName, StringComparison.OrdinalIgnoreCase));
                if (result != null) { return result; }

                result = filteredStations.FirstOrDefault(i => string.Equals(NormalizeName(i.Name ?? string.Empty), channelName, StringComparison.OrdinalIgnoreCase));
                if (result != null) { return result; }
            }
            return station;
        }

        private string NormalizeName(string value)
        {
            return value.Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        public async Task AddMetadata(ListingsProviderInfo info, List<ChannelInfo> channels, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(info.ListingsId))
            {
                throw new Exception("ListingsId required");
            }

            _stations.Clear();

            var stations = await GetStations(info, cancellationToken);

            foreach (var channel in channels)
            {
                var station = GetStation(channel, stations);
                if (station != null)
                {
                    var stationId = info.Id + "_" + station.Id;
                    _stations.TryAdd(stationId, station);
                    if (station.ImageUrl != null)
                    {
                        channel.ImageUrl = station.ImageUrl;
                        channel.HasImage = true;
                    }
                    channel.Name = station.Name ?? station.Callsign;
                    channel.StationId = stationId;
                }
                else
                {
                    _logger.Info("Listing provider doesnt have data for channel: " + channel.Number + " " + channel.Name);
                }

            }
            _logger.Info("Added " + _stations.Count + " stations to the dictionary");
        }
    }
    public class Station
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Callsign { get; set; }
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the number.
        /// </summary>
        /// <value>The number.</value>
        public List<string> ChannelNumbers { get; set; }

        /// <summary>
        /// Get or sets the Id.
        /// </summary>
        /// <value>The id of the channel.</value>
        public string Id { get; set; }

        /// <summary>
        /// Get or sets the Affiliate
        /// </summary>
        /// <value>The id of the channel.</value>
        public string Affiliate { get; set; }

        /// <summary>
        /// Supply the image url if it can be downloaded
        /// </summary>
        /// <value>The image URL.</value>
        public string ImageUrl { get; set; }

    }
}