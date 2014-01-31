﻿using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Localization;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Providers.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.BoxSets
{
    public class BoxSetMetadataService : ConcreteMetadataService<BoxSet, ItemId>
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILocalizationManager _iLocalizationManager;

        public BoxSetMetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IProviderRepository providerRepo, ILibraryManager libraryManager, ILocalizationManager iLocalizationManager)
            : base(serverConfigurationManager, logger, providerManager, providerRepo)
        {
            _libraryManager = libraryManager;
            _iLocalizationManager = iLocalizationManager;
        }

        /// <summary>
        /// Merges the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="lockedFields">The locked fields.</param>
        /// <param name="replaceData">if set to <c>true</c> [replace data].</param>
        protected override void MergeData(BoxSet source, BoxSet target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings)
        {
            ProviderUtils.MergeBaseItemData(source, target, lockedFields, replaceData, mergeMetadataSettings);
        }

        protected override Task SaveItem(BoxSet item, ItemUpdateType reason, CancellationToken cancellationToken)
        {
            return _libraryManager.UpdateItem(item, reason, cancellationToken);
        }

        protected override ItemUpdateType AfterMetadataRefresh(BoxSet item)
        {
            var updateType = base.AfterMetadataRefresh(item);

            if (!item.LockedFields.Contains(MetadataFields.OfficialRating))
            {
                var currentOfficialRating = item.OfficialRating;

                // Gather all possible ratings
                var ratings = item.RecursiveChildren
                    .Concat(item.GetLinkedChildren())
                    .Where(i => i is Movie || i is Series)
                    .Select(i => i.OfficialRating)
                    .Where(i => !string.IsNullOrEmpty(i))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(i => new Tuple<string, int?>(i, _iLocalizationManager.GetRatingLevel(i)))
                    .OrderBy(i => i.Item2 ?? 1000)
                    .Select(i => i.Item1);

                item.OfficialRating = ratings.FirstOrDefault() ?? item.OfficialRating;

                if (!string.Equals(currentOfficialRating ?? string.Empty, item.OfficialRating ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
                {
                    updateType = updateType | ItemUpdateType.MetadataDownload;
                }
            }

            return updateType;
        }
    }
}
