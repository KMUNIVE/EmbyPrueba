﻿using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Session;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Api.Playback
{
    [Route("/Items/{Id}/MediaInfo", "GET", Summary = "Gets live playback media info for an item")]
    public class GetLiveMediaInfo : IReturn<PlaybackInfoResponse>
    {
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string Id { get; set; }

        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string UserId { get; set; }
    }

    [Route("/Items/{Id}/PlaybackInfo", "GET", Summary = "Gets live playback media info for an item")]
    public class GetPlaybackInfo : IReturn<PlaybackInfoResponse>
    {
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string Id { get; set; }

        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string UserId { get; set; }
    }

    [Route("/Items/{Id}/PlaybackInfo", "POST", Summary = "Gets live playback media info for an item")]
    public class GetPostedPlaybackInfo : PlaybackInfoRequest, IReturn<PlaybackInfoResponse>
    {
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string Id { get; set; }

        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string UserId { get; set; }

        [ApiMember(Name = "MaxStreamingBitrate", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int? MaxStreamingBitrate { get; set; }

        [ApiMember(Name = "StartTimeTicks", Description = "Optional. Specify a starting offset, in ticks. 1 tick = 10000 ms", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public long? StartTimeTicks { get; set; }

        [ApiMember(Name = "AudioStreamIndex", Description = "Optional. The index of the audio stream to use. If omitted the first audio stream will be used.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int? AudioStreamIndex { get; set; }

        [ApiMember(Name = "SubtitleStreamIndex", Description = "Optional. The index of the subtitle stream to use. If omitted no subtitles will be used.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int? SubtitleStreamIndex { get; set; }

        [ApiMember(Name = "MediaSourceId", Description = "The media version id, if playing an alternate version", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string MediaSourceId { get; set; }
    }

    [Route("/MediaSources/Open", "POST", Summary = "Opens a media source")]
    public class OpenMediaSource : IReturn<MediaSourceInfo>
    {
        [ApiMember(Name = "OpenToken", Description = "OpenToken", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string OpenToken { get; set; }
    }

    [Route("/MediaSources/Close", "POST", Summary = "Closes a media source")]
    public class CloseMediaSource : IReturnVoid
    {
        [ApiMember(Name = "LiveStreamId", Description = "LiveStreamId", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string LiveStreamId { get; set; }
    }

    [Authenticated]
    public class MediaInfoService : BaseApiService
    {
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly IDeviceManager _deviceManager;
        private readonly ILibraryManager _libraryManager;

        public MediaInfoService(IMediaSourceManager mediaSourceManager, IDeviceManager deviceManager, ILibraryManager libraryManager)
        {
            _mediaSourceManager = mediaSourceManager;
            _deviceManager = deviceManager;
            _libraryManager = libraryManager;
        }

        public async Task<object> Get(GetPlaybackInfo request)
        {
            var result = await GetPlaybackInfo(request.Id, request.UserId).ConfigureAwait(false);
            return ToOptimizedResult(result);
        }

        public async Task<object> Get(GetLiveMediaInfo request)
        {
            var result = await GetPlaybackInfo(request.Id, request.UserId).ConfigureAwait(false);
            return ToOptimizedResult(result);
        }

        public async Task<object> Post(OpenMediaSource request)
        {
            var result = await _mediaSourceManager.OpenLiveStream(request.OpenToken, false, CancellationToken.None).ConfigureAwait(false);
            return ToOptimizedResult(result);
        }

        public void Post(CloseMediaSource request)
        {
            var task = _mediaSourceManager.CloseLiveStream(request.LiveStreamId, CancellationToken.None);
            Task.WaitAll(task);
        }

        public async Task<object> Post(GetPostedPlaybackInfo request)
        {
            var info = await GetPlaybackInfo(request.Id, request.UserId, request.MediaSourceId).ConfigureAwait(false);
            var authInfo = AuthorizationContext.GetAuthorizationInfo(Request);

            var profile = request.DeviceProfile;
            if (profile == null)
            {
                var caps = _deviceManager.GetCapabilities(authInfo.DeviceId);
                if (caps != null)
                {
                    profile = caps.DeviceProfile;
                }
            }

            if (profile != null)
            {
                var mediaSourceId = request.MediaSourceId;
                SetDeviceSpecificData(request.Id, info, profile, authInfo, request.MaxStreamingBitrate, request.StartTimeTicks ?? 0, mediaSourceId, request.AudioStreamIndex, request.SubtitleStreamIndex);
            }

            return ToOptimizedResult(info);
        }

        private async Task<PlaybackInfoResponse> GetPlaybackInfo(string id, string userId, string mediaSourceId = null)
        {
            var result = new PlaybackInfoResponse();

            IEnumerable<MediaSourceInfo> mediaSources;

            try
            {
                mediaSources = await _mediaSourceManager.GetPlayackMediaSources(id, userId, true, CancellationToken.None).ConfigureAwait(false);
            }
            catch (PlaybackException ex)
            {
                mediaSources = new List<MediaSourceInfo>();
                result.ErrorCode = ex.ErrorCode;
            }

            result.MediaSources = mediaSources.ToList();

            if (!string.IsNullOrWhiteSpace(mediaSourceId))
            {
                result.MediaSources = result.MediaSources
                    .Where(i => string.Equals(i.Id, mediaSourceId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (result.MediaSources.Count == 0)
            {
                if (!result.ErrorCode.HasValue)
                {
                    result.ErrorCode = PlaybackErrorCode.NoCompatibleStream;
                }
            }
            else
            {
                result.StreamId = Guid.NewGuid().ToString("N");
            }

            return result;
        }

        private void SetDeviceSpecificData(string itemId, 
            PlaybackInfoResponse result, 
            DeviceProfile profile, 
            AuthorizationInfo auth, 
            int? maxBitrate, 
            long startTimeTicks,
            string mediaSourceId,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            var item = _libraryManager.GetItemById(itemId);

            foreach (var mediaSource in result.MediaSources)
            {
                SetDeviceSpecificData(item, mediaSource, profile, auth, maxBitrate, startTimeTicks, mediaSourceId, audioStreamIndex, subtitleStreamIndex);
            }

            SortMediaSources(result);
        }

        private void SetDeviceSpecificData(BaseItem item,
            MediaSourceInfo mediaSource,
            DeviceProfile profile,
            AuthorizationInfo auth,
            int? maxBitrate,
            long startTimeTicks,
            string mediaSourceId,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            var streamBuilder = new StreamBuilder();

            var options = new VideoOptions
            {
                MediaSources = new List<MediaSourceInfo> { mediaSource },
                Context = EncodingContext.Streaming,
                DeviceId = auth.DeviceId,
                ItemId = item.Id.ToString("N"),
                Profile = profile,
                MaxBitrate = maxBitrate
            };

            if (string.Equals(mediaSourceId, mediaSource.Id, StringComparison.OrdinalIgnoreCase))
            {
                options.MediaSourceId = mediaSourceId;
                options.AudioStreamIndex = audioStreamIndex;
                options.SubtitleStreamIndex = subtitleStreamIndex;
            }

            if (mediaSource.SupportsDirectPlay)
            {
                var supportsDirectStream = mediaSource.SupportsDirectStream;

                // Dummy this up to fool StreamBuilder
                mediaSource.SupportsDirectStream = true;

                // The MediaSource supports direct stream, now test to see if the client supports it
                var streamInfo = string.Equals(item.MediaType, MediaType.Audio, StringComparison.OrdinalIgnoreCase) ?
                    streamBuilder.BuildAudioItem(options) :
                    streamBuilder.BuildVideoItem(options);

                if (streamInfo == null || !streamInfo.IsDirectStream)
                {
                    mediaSource.SupportsDirectPlay = false;
                }

                // Set this back to what it was
                mediaSource.SupportsDirectStream = supportsDirectStream;
            }

            if (mediaSource.SupportsDirectStream)
            {
                // The MediaSource supports direct stream, now test to see if the client supports it
                var streamInfo = string.Equals(item.MediaType, MediaType.Audio, StringComparison.OrdinalIgnoreCase) ?
                    streamBuilder.BuildAudioItem(options) :
                    streamBuilder.BuildVideoItem(options);

                if (streamInfo == null || !streamInfo.IsDirectStream)
                {
                    mediaSource.SupportsDirectStream = false;
                }
            }

            if (mediaSource.SupportsTranscoding)
            {
                // The MediaSource supports direct stream, now test to see if the client supports it
                var streamInfo = string.Equals(item.MediaType, MediaType.Audio, StringComparison.OrdinalIgnoreCase) ?
                    streamBuilder.BuildAudioItem(options) :
                    streamBuilder.BuildVideoItem(options);

                if (streamInfo != null && streamInfo.PlayMethod == PlayMethod.Transcode)
                {
                    streamInfo.StartPositionTicks = startTimeTicks;
                    mediaSource.TranscodingUrl = streamInfo.ToUrl("-", auth.Token).Substring(1);
                    mediaSource.TranscodingContainer = streamInfo.Container;
                    mediaSource.TranscodingSubProtocol = streamInfo.SubProtocol;
                }
            }
        }

        private void SortMediaSources(PlaybackInfoResponse result)
        {
            var originalList = result.MediaSources.ToList();

            result.MediaSources = result.MediaSources.OrderBy(i =>
            {
                // Nothing beats direct playing a file
                if (i.SupportsDirectPlay && i.Protocol == MediaProtocol.File)
                {
                    return 0;
                }

                return 1;

            }).ThenBy(i =>
            {
                // Let's assume direct streaming a file is just as desirable as direct playing a remote url
                if (i.SupportsDirectPlay || i.SupportsDirectStream)
                {
                    return 0;
                }

                return 1;

            }).ThenBy(i =>
            {
                switch (i.Protocol)
                {
                    case MediaProtocol.File:
                        return 0;
                    default:
                        return 1;
                }

            }).ThenBy(originalList.IndexOf)
            .ToList();
        }
    }
}
