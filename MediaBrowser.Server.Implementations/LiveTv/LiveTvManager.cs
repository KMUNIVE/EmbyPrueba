﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Server.Implementations.LiveTv
{
    /// <summary>
    /// Class LiveTvManager
    /// </summary>
    public class LiveTvManager : ILiveTvManager
    {
        private readonly IServerApplicationPaths _appPaths;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly IItemRepository _itemRepo;
        private readonly IImageProcessor _imageProcessor;

        private readonly List<ILiveTvService> _services = new List<ILiveTvService>();

        private List<Channel> _channels = new List<Channel>();
        private List<ProgramInfoDto> _programs = new List<ProgramInfoDto>();

        public LiveTvManager(IServerApplicationPaths appPaths, IFileSystem fileSystem, ILogger logger, IItemRepository itemRepo, IImageProcessor imageProcessor)
        {
            _appPaths = appPaths;
            _fileSystem = fileSystem;
            _logger = logger;
            _itemRepo = itemRepo;
            _imageProcessor = imageProcessor;
        }

        /// <summary>
        /// Gets the services.
        /// </summary>
        /// <value>The services.</value>
        public IReadOnlyList<ILiveTvService> Services
        {
            get { return _services; }
        }

        /// <summary>
        /// Adds the parts.
        /// </summary>
        /// <param name="services">The services.</param>
        public void AddParts(IEnumerable<ILiveTvService> services)
        {
            _services.AddRange(services);
        }

        /// <summary>
        /// Gets the channel info dto.
        /// </summary>
        /// <param name="info">The info.</param>
        /// <returns>ChannelInfoDto.</returns>
        public ChannelInfoDto GetChannelInfoDto(Channel info)
        {
            return new ChannelInfoDto
            {
                Name = info.Name,
                ServiceName = info.ServiceName,
                ChannelType = info.ChannelType,
                Number = info.ChannelNumber,
                PrimaryImageTag = GetLogoImageTag(info),
                Type = info.GetType().Name,
                Id = info.Id.ToString("N"),
                MediaType = info.MediaType
            };
        }

        private ILiveTvService GetService(ChannelInfo channel)
        {
            return _services.FirstOrDefault(i => string.Equals(channel.ServiceName, i.Name, StringComparison.OrdinalIgnoreCase));
        }

        private Guid? GetLogoImageTag(Channel info)
        {
            var path = info.PrimaryImagePath;

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                return _imageProcessor.GetImageCacheTag(info, ImageType.Primary, path);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting channel image info for {0}", ex, info.Name);
            }

            return null;
        }

        public QueryResult<ChannelInfoDto> GetChannels(ChannelQuery query)
        {
            var channels = _channels.OrderBy(i =>
            {
                double number = 0;

                if (!string.IsNullOrEmpty(i.ChannelNumber))
                {
                    double.TryParse(i.ChannelNumber, out number);
                }

                return number;

            }).ThenBy(i => i.Name)
            .Select(GetChannelInfoDto)
            .ToArray();

            return new QueryResult<ChannelInfoDto>
            {
                Items = channels,
                TotalRecordCount = channels.Length
            };
        }

        public Channel GetChannel(string id)
        {
            var guid = new Guid(id);

            return _channels.FirstOrDefault(i => i.Id == guid);
        }

        internal async Task RefreshChannels(IProgress<double> progress, CancellationToken cancellationToken)
        {
            // Avoid implicitly captured closure
            var currentCancellationToken = cancellationToken;

            var channelTasks = _services.Select(i => i.GetChannelsAsync(currentCancellationToken));

            progress.Report(10);

            var results = await Task.WhenAll(channelTasks).ConfigureAwait(false);

            var allChannels = results.SelectMany(i => i).ToList();

            var list = new List<Channel>();
            var programs = new List<ProgramInfoDto>();

            var numComplete = 0;

            foreach (var channelInfo in allChannels)
            {
                try
                {
                    var item = await GetChannel(channelInfo, cancellationToken).ConfigureAwait(false);

                    var service = GetService(channelInfo);

                    var channelPrograms = await service.GetProgramsAsync(channelInfo.Id, cancellationToken).ConfigureAwait(false);

                    programs.AddRange(channelPrograms.Select(program => GetProgramInfoDto(program, item)));

                    list.Add(item);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error getting channel information for {0}", ex, channelInfo.Name);
                }

                numComplete++;
                double percent = numComplete;
                percent /= allChannels.Count;

                progress.Report(90 * percent + 10);
            }

            _programs = programs;
            _channels = list;
        }

        private ProgramInfoDto GetProgramInfoDto(ProgramInfo program, Channel channel)
        {
            var id = channel.ServiceName + channel.ChannelId + program.Id;
            id = id.GetMD5().ToString("N");

            return new ProgramInfoDto
            {
                ChannelId = channel.Id.ToString("N"),
                Description = program.Description,
                EndDate = program.EndDate,
                Genres = program.Genres,
                ExternalId = program.Id,
                Id = id,
                Name = program.Name,
                ServiceName = channel.ServiceName,
                StartDate = program.StartDate
            };
        }

        private async Task<Channel> GetChannel(ChannelInfo channelInfo, CancellationToken cancellationToken)
        {
            var path = Path.Combine(_appPaths.ItemsByNamePath, "channels", _fileSystem.GetValidFilename(channelInfo.ServiceName), _fileSystem.GetValidFilename(channelInfo.Name));

            var fileInfo = new DirectoryInfo(path);

            var isNew = false;

            if (!fileInfo.Exists)
            {
                Directory.CreateDirectory(path);
                fileInfo = new DirectoryInfo(path);

                if (!fileInfo.Exists)
                {
                    throw new IOException("Path not created: " + path);
                }

                isNew = true;
            }

            var type = typeof(Channel);

            var id = (path + channelInfo.Number).GetMBId(type);

            var item = _itemRepo.RetrieveItem(id) as Channel;

            if (item == null)
            {
                item = new Channel
                {
                    Name = channelInfo.Name,
                    Id = id,
                    DateCreated = _fileSystem.GetCreationTimeUtc(fileInfo),
                    DateModified = _fileSystem.GetLastWriteTimeUtc(fileInfo),
                    Path = path,
                    ChannelId = channelInfo.Id,
                    ChannelNumber = channelInfo.Number,
                    ServiceName = channelInfo.ServiceName
                };

                isNew = true;
            }

            // Set this now so we don't cause additional file system access during provider executions
            item.ResetResolveArgs(fileInfo);

            await item.RefreshMetadata(cancellationToken, forceSave: isNew, resetResolveArgs: false);

            return item;
        }

        public QueryResult<ProgramInfoDto> GetPrograms(ProgramQuery query)
        {
            IEnumerable<ProgramInfoDto> programs = _programs
                .OrderBy(i => i.StartDate)
                .ThenBy(i => i.EndDate);

            if (!string.IsNullOrEmpty(query.ServiceName))
            {
                programs = programs.Where(i => string.Equals(i.ServiceName, query.ServiceName, StringComparison.OrdinalIgnoreCase));
            }

            if (query.ChannelIdList.Length > 0)
            {
                var guids = query.ChannelIdList.Select(i => new Guid(i)).ToList();

                programs = programs.Where(i => guids.Contains(new Guid(i.ChannelId)));
            }

            var returnArray = programs.ToArray();

            return new QueryResult<ProgramInfoDto>
            {
                Items = returnArray,
                TotalRecordCount = returnArray.Length
            };
        }
    }
}
