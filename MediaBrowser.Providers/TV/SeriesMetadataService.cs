﻿using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Localization;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Providers.Manager;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.TV
{
    public class SeriesMetadataService : MetadataService<Series, SeriesInfo>
    {
        private readonly ILocalizationManager _localization;
        private readonly ILibraryManager _libraryManager;

        public SeriesMetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IProviderRepository providerRepo, IFileSystem fileSystem, IUserDataManager userDataManager, ILocalizationManager localization, ILibraryManager libraryManager)
            : base(serverConfigurationManager, logger, providerManager, providerRepo, fileSystem, userDataManager)
        {
            _localization = localization;
            _libraryManager = libraryManager;
        }

        /// <summary>
        /// Merges the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="lockedFields">The locked fields.</param>
        /// <param name="replaceData">if set to <c>true</c> [replace data].</param>
        /// <param name="mergeMetadataSettings">if set to <c>true</c> [merge metadata settings].</param>
        protected override void MergeData(Series source, Series target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings)
        {
            ProviderUtils.MergeBaseItemData(source, target, lockedFields, replaceData, mergeMetadataSettings);

            if (replaceData || target.SeasonCount == 0)
            {
                target.SeasonCount = source.SeasonCount;
            }

            if (replaceData || string.IsNullOrEmpty(target.AirTime))
            {
                target.AirTime = source.AirTime;
            }

            if (replaceData || !target.Status.HasValue)
            {
                target.Status = source.Status;
            }

            if (replaceData || target.AirDays == null || target.AirDays.Count == 0)
            {
                target.AirDays = source.AirDays;
            }

            if (mergeMetadataSettings)
            {
                target.DisplaySpecialsWithSeasons = source.DisplaySpecialsWithSeasons;
            }
        }

        protected override async Task AfterMetadataRefresh(Series item, MetadataRefreshOptions refreshOptions, CancellationToken cancellationToken)
        {
            await base.AfterMetadataRefresh(item, refreshOptions, cancellationToken).ConfigureAwait(false);

            if (refreshOptions.IsPostRecursiveRefresh)
            {
                var provider = new DummySeasonProvider(ServerConfigurationManager, Logger, _localization, _libraryManager);

                await provider.Run(item, CancellationToken.None).ConfigureAwait(false);
            }
        }

        protected override bool IsFullLocalMetadata(Series item)
        {
            if (string.IsNullOrWhiteSpace(item.Overview))
            {
                return false;
            }
            if (!item.ProductionYear.HasValue)
            {
                return false;
            }
            return base.IsFullLocalMetadata(item);
        }
    }
}
