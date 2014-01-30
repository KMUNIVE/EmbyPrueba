﻿using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Providers.Manager;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.MusicGenres
{
    public class MusicGenreMetadataService : MetadataService<MusicGenre>
    {
        private readonly ILibraryManager _libraryManager;

        public MusicGenreMetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IProviderRepository providerRepo, ILibraryManager libraryManager)
            : base(serverConfigurationManager, logger, providerManager, providerRepo)
        {
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
        protected override void MergeData(MusicGenre source, MusicGenre target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings)
        {
            ProviderUtils.MergeBaseItemData(source, target, lockedFields, replaceData, mergeMetadataSettings);
        }

        protected override Task SaveItem(MusicGenre item, ItemUpdateType reason, CancellationToken cancellationToken)
        {
            return _libraryManager.UpdateItem(item, reason, cancellationToken);
        }
    }
}
