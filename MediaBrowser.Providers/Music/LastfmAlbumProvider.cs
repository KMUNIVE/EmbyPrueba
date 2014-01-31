﻿using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Music
{
    public class LastfmAlbumProvider : IRemoteMetadataProvider<MusicAlbum>, IHasOrder
    {
        private readonly IJsonSerializer _json;
        private readonly IHttpClient _httpClient;

        private readonly IServerConfigurationManager _config;
        private readonly ILogger _logger;

        public LastfmAlbumProvider(IHttpClient httpClient, IJsonSerializer json, IServerConfigurationManager config, ILogger logger)
        {
            _httpClient = httpClient;
            _json = json;
            _config = config;
            _logger = logger;
        }

        public async Task<MetadataResult<MusicAlbum>> GetMetadata(ItemId id, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<MusicAlbum>();

            var lastFmData = await GetAlbumResult((AlbumId)id, cancellationToken).ConfigureAwait(false);

            if (lastFmData != null && lastFmData.album != null)
            {
                result.HasMetadata = true;
                ProcessAlbumData(result.Item, lastFmData.album);
            }

            return result;
        }

        private async Task<LastfmGetAlbumResult> GetAlbumResult(AlbumId item, CancellationToken cancellationToken)
        {
            // Try album release Id
            if (!string.IsNullOrEmpty(item.GetProviderId(MetadataProviders.Musicbrainz)))
            {
                var result = await GetAlbumResult(item.GetProviderId(MetadataProviders.Musicbrainz), cancellationToken).ConfigureAwait(false);

                if (result != null && result.album != null)
                {
                    return result;
                }
            }

            // Try album release group Id
            if (!string.IsNullOrEmpty(item.GetProviderId(MetadataProviders.MusicBrainzReleaseGroup)))
            {
                var result = await GetAlbumResult(item.GetProviderId(MetadataProviders.MusicBrainzReleaseGroup), cancellationToken).ConfigureAwait(false);

                if (result != null && result.album != null)
                {
                    return result;
                }
            }

            //// Get each song, distinct by the combination of AlbumArtist and Album
            //var songs = item.RecursiveChildren.OfType<Audio>().DistinctBy(i => (i.AlbumArtist ?? string.Empty) + (i.Album ?? string.Empty), StringComparer.OrdinalIgnoreCase).ToList();

            //foreach (var song in songs.Where(song => !string.IsNullOrEmpty(song.Album) && !string.IsNullOrEmpty(song.AlbumArtist)))
            //{
            //    var result = await GetAlbumResult(song.AlbumArtist, song.Album, cancellationToken).ConfigureAwait(false);

            //    if (result != null && result.album != null)
            //    {
            //        return result;
            //    }
            //}

            if (string.IsNullOrEmpty(item.AlbumArtist))
            {
                return null;
            }

            return await GetAlbumResult(item.AlbumArtist, item.Name, cancellationToken);
        }

        private async Task<LastfmGetAlbumResult> GetAlbumResult(string artist, string album, CancellationToken cancellationToken)
        {
            // Get albu info using artist and album name
            var url = LastfmArtistProvider.RootUrl + string.Format("method=album.getInfo&artist={0}&album={1}&api_key={2}&format=json", UrlEncode(artist), UrlEncode(album), LastfmArtistProvider.ApiKey);

            using (var json = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = LastfmArtistProvider.LastfmResourcePool,
                CancellationToken = cancellationToken,
                EnableHttpCompression = false

            }).ConfigureAwait(false))
            {
                using (var reader = new StreamReader(json))
                {
                    var jsonText = await reader.ReadToEndAsync().ConfigureAwait(false);

                    // Fix their bad json
                    jsonText = jsonText.Replace("\"#text\"", "\"url\"");

                    return _json.DeserializeFromString<LastfmGetAlbumResult>(jsonText);
                }
            }
        }

        private async Task<LastfmGetAlbumResult> GetAlbumResult(string musicbraizId, CancellationToken cancellationToken)
        {
            // Get albu info using artist and album name
            var url = LastfmArtistProvider.RootUrl + string.Format("method=album.getInfo&mbid={0}&api_key={1}&format=json", musicbraizId, LastfmArtistProvider.ApiKey);

            using (var json = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                ResourcePool = LastfmArtistProvider.LastfmResourcePool,
                CancellationToken = cancellationToken,
                EnableHttpCompression = false

            }).ConfigureAwait(false))
            {
                return _json.DeserializeFromStream<LastfmGetAlbumResult>(json);
            }
        }

        private void ProcessAlbumData(MusicAlbum item, LastfmAlbum data)
        {
            var overview = data.wiki != null ? data.wiki.content : null;

            if (!item.LockedFields.Contains(MetadataFields.Overview))
            {
                item.Overview = overview;
            }

            // Only grab the date here if the album doesn't already have one, since id3 tags are preferred
            DateTime release;

            if (DateTime.TryParse(data.releasedate, out release))
            {
                // Lastfm sends back null as sometimes 1901, other times 0
                if (release.Year > 1901)
                {
                    if (!item.PremiereDate.HasValue)
                    {
                        item.PremiereDate = release;
                    }

                    if (!item.ProductionYear.HasValue)
                    {
                        item.ProductionYear = release.Year;
                    }
                }
            }

            string imageSize;
            var url = LastfmHelper.GetImageUrl(data, out imageSize);

            var musicBrainzId = item.GetProviderId(MetadataProviders.Musicbrainz) ??
                item.GetProviderId(MetadataProviders.MusicBrainzReleaseGroup);

            LastfmHelper.SaveImageInfo(_config.ApplicationPaths, _logger, musicBrainzId, url, imageSize);
        }

        /// <summary>
        /// Encodes an URL.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        private string UrlEncode(string name)
        {
            return WebUtility.UrlEncode(name);
        }

        public string Name
        {
            get { return "last.fm"; }
        }

        public int Order
        {
            get { return 1; }
        }
    }

    #region Result Objects

    public class LastfmStats
    {
        public string listeners { get; set; }
        public string playcount { get; set; }
    }

    public class LastfmTag
    {
        public string name { get; set; }
        public string url { get; set; }
    }


    public class LastfmTags
    {
        public List<LastfmTag> tag { get; set; }
    }

    public class LastfmFormationInfo
    {
        public string yearfrom { get; set; }
        public string yearto { get; set; }
    }

    public class LastFmBio
    {
        public string published { get; set; }
        public string summary { get; set; }
        public string content { get; set; }
        public string placeformed { get; set; }
        public string yearformed { get; set; }
        public List<LastfmFormationInfo> formationlist { get; set; }
    }

    public class LastFmImage
    {
        public string url { get; set; }
        public string size { get; set; }
    }

    public class LastfmArtist : IHasLastFmImages
    {
        public string name { get; set; }
        public string mbid { get; set; }
        public string url { get; set; }
        public string streamable { get; set; }
        public string ontour { get; set; }
        public LastfmStats stats { get; set; }
        public List<LastfmArtist> similar { get; set; }
        public LastfmTags tags { get; set; }
        public LastFmBio bio { get; set; }
        public List<LastFmImage> image { get; set; }
    }


    public class LastfmAlbum : IHasLastFmImages
    {
        public string name { get; set; }
        public string artist { get; set; }
        public string id { get; set; }
        public string mbid { get; set; }
        public string releasedate { get; set; }
        public int listeners { get; set; }
        public int playcount { get; set; }
        public LastfmTags toptags { get; set; }
        public LastFmBio wiki { get; set; }
        public List<LastFmImage> image { get; set; }
    }

    public interface IHasLastFmImages
    {
        List<LastFmImage> image { get; set; }
    }

    public class LastfmGetAlbumResult
    {
        public LastfmAlbum album { get; set; }
    }

    public class LastfmGetArtistResult
    {
        public LastfmArtist artist { get; set; }
    }

    public class Artistmatches
    {
        public List<LastfmArtist> artist { get; set; }
    }

    public class LastfmArtistSearchResult
    {
        public Artistmatches artistmatches { get; set; }
    }

    public class LastfmArtistSearchResults
    {
        public LastfmArtistSearchResult results { get; set; }
    }

    #endregion
}
