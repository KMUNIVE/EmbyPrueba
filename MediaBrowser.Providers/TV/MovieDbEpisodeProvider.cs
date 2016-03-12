﻿using CommonIO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Localization;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.TV
{
    class MovieDbEpisodeProvider :
            MovieDbProviderBase,
            IRemoteMetadataProvider<Episode, EpisodeInfo>,
            IHasOrder
    {
        public MovieDbEpisodeProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager)
            : base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager)
        { }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(new List<RemoteSearchResult>());
        }

        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            string seriesTmdbId;
            info.SeriesProviderIds.TryGetValue(MetadataProviders.Tmdb.ToString(), out seriesTmdbId);

            if (string.IsNullOrEmpty(seriesTmdbId))
            {
                return result;
            }

            var seasonNumber = info.ParentIndexNumber;
            var episodeNumber = info.IndexNumber;

            if (!seasonNumber.HasValue || !episodeNumber.HasValue)
            {
                return result;
            }

            try
            {
                var response = await GetEpisodeInfo(seriesTmdbId, seasonNumber.Value, episodeNumber.Value, info.MetadataLanguage, cancellationToken).ConfigureAwait(false);

                result.HasMetadata = true;

                var item = new Episode();
                result.Item = item;

                item.Name = info.Name;
                item.IndexNumber = info.IndexNumber;
                item.ParentIndexNumber = info.ParentIndexNumber;
                item.IndexNumberEnd = info.IndexNumberEnd;

                if (response.external_ids.tvdb_id > 0)
                {
                    item.SetProviderId(MetadataProviders.Tvdb, response.external_ids.tvdb_id.ToString(CultureInfo.InvariantCulture));
                }

                item.PremiereDate = response.air_date;
                item.ProductionYear = result.Item.PremiereDate.Value.Year;

                item.Name = response.name;
                item.Overview = response.overview;

                item.CommunityRating = (float)response.vote_average;
                item.VoteCount = response.vote_count;

                result.ResetPeople();

                var credits = response.credits;
                if (credits != null)
                {
                    //Actors, Directors, Writers - all in People
                    //actors come from cast
                    if (credits.cast != null)
                    {
                        foreach (var actor in credits.cast.OrderBy(a => a.order))
                        {
                            result.AddPerson(new PersonInfo { Name = actor.name.Trim(), Role = actor.character, Type = PersonType.Actor, SortOrder = actor.order });
                        }
                    }

                    // guest stars
                    if (credits.guest_stars != null)
                    {
                        foreach (var guest in credits.guest_stars.OrderBy(a => a.order))
                        {
                            result.AddPerson(new PersonInfo { Name = guest.name.Trim(), Role = guest.character, Type = PersonType.GuestStar, SortOrder = guest.order });
                        }
                    }

                    //and the rest from crew
                    if (credits.crew != null)
                    {
                        foreach (var person in credits.crew)
                        {
                            result.AddPerson(new PersonInfo { Name = person.name.Trim(), Role = person.job, Type = person.department });
                        }
                    }
                }
            }
            catch (HttpException ex)
            {
                Logger.Error("No metadata found for {0}", seasonNumber.Value);

                if (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.NotFound)
                {
                    return result;
                }

                throw;
            }

            return result;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return GetResponse(url, cancellationToken);
        }

        public int Order
        {
            get
            {
                // After TheTvDb
                return 1;
            }
        }

        public string Name
        {
            get { return "TheMovieDb"; }
        }
    }
}
