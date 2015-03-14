﻿using System.Collections.Generic;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Providers.Manager;

namespace MediaBrowser.Providers.LiveTv
{
    public class ProgramMetadataService : MetadataService<LiveTvProgram, LiveTvProgramLookupInfo>
    {
        public ProgramMetadataService(
            IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, 
            IProviderRepository providerRepo, IFileSystem fileSystem, IUserDataManager userDataManager) : base(
            serverConfigurationManager, logger, providerManager, providerRepo, fileSystem, userDataManager)
        {
        }

        /// <summary>
        /// Merges the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="lockedFields">The locked fields.</param>
        /// <param name="replaceData">if set to <c>true</c> [replace data].</param>
        /// <param name="mergeMetadataSettings"></param>
        protected override void MergeData(LiveTvProgram source, LiveTvProgram target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings)
        {
            if (target.IsMovie)
            {
                // Maybe only map a few of the items, alternatively, copy values out of the target we want to retain, merge, then put them back.
            }

            ProviderUtils.MergeBaseItemData(source, target, lockedFields, replaceData, mergeMetadataSettings);
        }
    }
}