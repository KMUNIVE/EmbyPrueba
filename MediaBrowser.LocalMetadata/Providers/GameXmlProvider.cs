﻿using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.LocalMetadata.Parsers;
using MediaBrowser.Model.Logging;
using System.IO;
using System.Threading;

namespace MediaBrowser.LocalMetadata.Providers
{
    public class GameXmlProvider : BaseXmlProvider<Game>
    {
        private readonly ILogger _logger;

        public GameXmlProvider(IFileSystem fileSystem, ILogger logger)
            : base(fileSystem)
        {
            _logger = logger;
        }

        protected override void Fetch(MetadataResult<Game> result, string path, CancellationToken cancellationToken)
        {
            new GameXmlParser(_logger).Fetch(result, path, cancellationToken);
        }

        protected override FileSystemInfo GetXmlFile(ItemInfo info, IDirectoryService directoryService)
        {
            var specificFile = Path.ChangeExtension(info.Path, ".xml");
            var file = new FileInfo(specificFile);

            return info.IsInMixedFolder || file.Exists ? file : new FileInfo(Path.Combine(Path.GetDirectoryName(info.Path), "game.xml"));
        }
    }
}
