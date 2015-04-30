﻿using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Providers.Manager;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.BoxSets
{
    public class BoxSetMetadataService : MetadataService<BoxSet, BoxSetInfo>
    {
        public BoxSetMetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IProviderRepository providerRepo, IFileSystem fileSystem, IUserDataManager userDataManager) : base(serverConfigurationManager, logger, providerManager, providerRepo, fileSystem, userDataManager)
        {
        }

        /// <summary>
        /// Merges the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="lockedFields">The locked fields.</param>
        /// <param name="replaceData">if set to <c>true</c> [replace data].</param>
        /// <param name="mergeMetadataSettings">if set to <c>true</c> [merge metadata settings].</param>
        protected override void MergeData(BoxSet source, BoxSet target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings)
        {
            ProviderUtils.MergeBaseItemData(source, target, lockedFields, replaceData, mergeMetadataSettings);

            if (mergeMetadataSettings)
            {
                var list = source.LinkedChildren.Where(i => i.Type != LinkedChildType.Manual).ToList();

                list.AddRange(target.LinkedChildren.Where(i => i.Type == LinkedChildType.Manual));

                target.LinkedChildren = list;
                target.Shares = source.Shares;
            }
        }

        protected override async Task<ItemUpdateType> BeforeSave(BoxSet item, bool isFullRefresh, ItemUpdateType currentUpdateType)
        {
            var updateType = await base.BeforeSave(item, isFullRefresh, currentUpdateType).ConfigureAwait(false);

            if (isFullRefresh || currentUpdateType > ItemUpdateType.None)
            {
                if (!item.LockedFields.Contains(MetadataFields.OfficialRating))
                {
                    if (item.UpdateRatingToContent())
                    {
                        updateType = updateType | ItemUpdateType.MetadataEdit;
                    }
                }
            }

            return updateType;
        }
    }
}
