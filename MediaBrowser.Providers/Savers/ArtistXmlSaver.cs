﻿using System.Collections.Generic;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.Movies;
using System;
using System.IO;
using System.Text;
using System.Threading;
using MediaBrowser.Providers.Music;

namespace MediaBrowser.Providers.Savers
{
    class ArtistXmlSaver : IMetadataSaver
    {
        private readonly IServerConfigurationManager _config;

        public ArtistXmlSaver(IServerConfigurationManager config)
        {
            _config = config;
        }

        /// <summary>
        /// Determines whether [is enabled for] [the specified item].
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="updateType">Type of the update.</param>
        /// <returns><c>true</c> if [is enabled for] [the specified item]; otherwise, <c>false</c>.</returns>
        public bool IsEnabledFor(BaseItem item, ItemUpdateType updateType)
        {
            // If new metadata has been downloaded and save local is on, OR metadata was manually edited, proceed
            if ((_config.Configuration.SaveLocalMeta && (updateType & ItemUpdateType.MetadataDownload) == ItemUpdateType.MetadataDownload)
                || (updateType & ItemUpdateType.MetadataEdit) == ItemUpdateType.MetadataEdit)
            {
                if (item is MusicArtist)
                {
                    return true;
                }
            }

            // If new metadata has been downloaded or metadata was manually edited, proceed
            if ((updateType & ItemUpdateType.MetadataDownload) == ItemUpdateType.MetadataDownload
                || (updateType & ItemUpdateType.MetadataEdit) == ItemUpdateType.MetadataEdit)
            {
                if (item is Artist)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Saves the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public void Save(BaseItem item, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();

            builder.Append("<Item>");

            XmlSaverHelpers.AddCommonNodes(item, builder);

            builder.Append("</Item>");

            var xmlFilePath = GetSavePath(item);

            XmlSaverHelpers.Save(builder, xmlFilePath, new List<string> { });

            // Set last refreshed so that the provider doesn't trigger after the file save
            ArtistProviderFromXml.Current.SetLastRefreshed(item, DateTime.UtcNow);
        }

        /// <summary>
        /// Gets the save path.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        public string GetSavePath(BaseItem item)
        {
            return Path.Combine(item.Path, "artist.xml");
        }
    }
}
