﻿using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace MediaBrowser.Providers.TV
{
    public class ManualTvdbEpisodeImageProvider : IImageProvider
    {
        private readonly IServerConfigurationManager _config;
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public ManualTvdbEpisodeImageProvider(IServerConfigurationManager config)
        {
            _config = config;
        }

        public string Name
        {
            get { return "TheTVDB"; }
        }

        public bool Supports(BaseItem item)
        {
            return item is Episode;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, ImageType imageType, CancellationToken cancellationToken)
        {
            var images = await GetAllImages(item, cancellationToken).ConfigureAwait(false);

            return images.Where(i => i.Type == imageType);
        }

        public Task<IEnumerable<RemoteImageInfo>> GetAllImages(BaseItem item, CancellationToken cancellationToken)
        {
            var episode = (Episode)item;

            var seriesId = episode.Series != null ? episode.Series.GetProviderId(MetadataProviders.Tvdb) : null;

            if (!string.IsNullOrEmpty(seriesId))
            {
                // Process images
                var seriesDataPath = TvdbSeriesProvider.GetSeriesDataPath(_config.ApplicationPaths, seriesId);

                var files = TvdbEpisodeProvider.Current.GetEpisodeXmlFiles(episode, seriesDataPath);

                var result = files.Select(i => GetImageInfo(i, cancellationToken))
                    .Where(i => i != null);

                return Task.FromResult(result);
            }

            return Task.FromResult<IEnumerable<RemoteImageInfo>>(new RemoteImageInfo[] { });
        }

        private RemoteImageInfo GetImageInfo(FileInfo xmlFile, CancellationToken cancellationToken)
        {
            var height = 225;
            var width = 400;
            var url = string.Empty;

            using (var streamReader = new StreamReader(xmlFile.FullName, Encoding.UTF8))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, new XmlReaderSettings
                {
                    CheckCharacters = false,
                    IgnoreProcessingInstructions = true,
                    IgnoreComments = true,
                    ValidationType = ValidationType.None
                }))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name)
                            {
                                case "thumb_width":
                                    {
                                        var val = reader.ReadElementContentAsString();

                                        if (!string.IsNullOrWhiteSpace(val))
                                        {
                                            int rval;

                                            // int.TryParse is local aware, so it can be probamatic, force us culture
                                            if (int.TryParse(val, NumberStyles.Integer, _usCulture, out rval))
                                            {
                                                width = rval;
                                            }
                                        }
                                        break;
                                    }

                                case "thumb_height":
                                    {
                                        var val = reader.ReadElementContentAsString();

                                        if (!string.IsNullOrWhiteSpace(val))
                                        {
                                            int rval;

                                            // int.TryParse is local aware, so it can be probamatic, force us culture
                                            if (int.TryParse(val, NumberStyles.Integer, _usCulture, out rval))
                                            {
                                                height = rval;
                                            }
                                        }
                                        break;
                                    }

                                case "filename":
                                    {
                                        var val = reader.ReadElementContentAsString();
                                        if (!string.IsNullOrWhiteSpace(val))
                                        {
                                            url = TVUtils.BannerUrl + val;
                                        }
                                        break;
                                    }

                                default:
                                    reader.Skip();
                                    break;
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            return new RemoteImageInfo
            {
                Width = width,
                Height = height,
                ProviderName = Name,
                Url = url,
                Type = ImageType.Primary
            };
        }

        public int Priority
        {
            get { return 0; }
        }
    }
}
