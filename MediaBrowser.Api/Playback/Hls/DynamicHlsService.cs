﻿using MediaBrowser.Common.IO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Dlna;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.IO;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MimeTypes = MediaBrowser.Model.Net.MimeTypes;

namespace MediaBrowser.Api.Playback.Hls
{
    /// <summary>
    /// Options is needed for chromecast. Threw Head in there since it's related
    /// </summary>
    [Route("/Videos/{Id}/master.m3u8", "GET", Summary = "Gets a video stream using HTTP live streaming.")]
    [Route("/Videos/{Id}/master.m3u8", "HEAD", Summary = "Gets a video stream using HTTP live streaming.")]
    public class GetMasterHlsVideoStream : VideoStreamRequest
    {
        public bool EnableAdaptiveBitrateStreaming { get; set; }

        public GetMasterHlsVideoStream()
        {
            EnableAdaptiveBitrateStreaming = true;
        }
    }

    [Route("/Videos/{Id}/main.m3u8", "GET", Summary = "Gets a video stream using HTTP live streaming.")]
    public class GetMainHlsVideoStream : VideoStreamRequest
    {
    }

    /// <summary>
    /// Class GetHlsVideoSegment
    /// </summary>
    [Route("/Videos/{Id}/hlsdynamic/{PlaylistId}/{SegmentId}.ts", "GET")]
    [Api(Description = "Gets an Http live streaming segment file. Internal use only.")]
    public class GetDynamicHlsVideoSegment : VideoStreamRequest
    {
        public string PlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the segment id.
        /// </summary>
        /// <value>The segment id.</value>
        public string SegmentId { get; set; }
    }

    public class DynamicHlsService : BaseHlsService
    {
        public DynamicHlsService(IServerConfigurationManager serverConfig, IUserManager userManager, ILibraryManager libraryManager, IIsoManager isoManager, IMediaEncoder mediaEncoder, IFileSystem fileSystem, ILiveTvManager liveTvManager, IDlnaManager dlnaManager, ISubtitleEncoder subtitleEncoder, IDeviceManager deviceManager, IMediaSourceManager mediaSourceManager, IZipClient zipClient, INetworkManager networkManager) : base(serverConfig, userManager, libraryManager, isoManager, mediaEncoder, fileSystem, liveTvManager, dlnaManager, subtitleEncoder, deviceManager, mediaSourceManager, zipClient)
        {
            NetworkManager = networkManager;
        }

        protected INetworkManager NetworkManager { get; private set; }

        public Task<object> Get(GetMasterHlsVideoStream request)
        {
            return GetAsync(request, "GET");
        }

        public Task<object> Head(GetMasterHlsVideoStream request)
        {
            return GetAsync(request, "HEAD");
        }

        public Task<object> Get(GetMainHlsVideoStream request)
        {
            return GetPlaylistAsync(request, "main");
        }

        public Task<object> Get(GetDynamicHlsVideoSegment request)
        {
            return GetDynamicSegment(request, request.SegmentId);
        }

        private async Task<object> GetDynamicSegment(VideoStreamRequest request, string segmentId)
        {
            if ((request.StartTimeTicks ?? 0) > 0)
            {
                throw new ArgumentException("StartTimeTicks is not allowed.");
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var requestedIndex = int.Parse(segmentId, NumberStyles.Integer, UsCulture);

            var state = await GetState(request, cancellationToken).ConfigureAwait(false);

            var playlistPath = Path.ChangeExtension(state.OutputFilePath, ".m3u8");

            var segmentPath = GetSegmentPath(playlistPath, requestedIndex);
            var segmentLength = state.SegmentLength;

            var segmentExtension = GetSegmentFileExtension(state);

            TranscodingJob job = null;

            if (File.Exists(segmentPath))
            {
                job = ApiEntryPoint.Instance.OnTranscodeBeginRequest(playlistPath, TranscodingJobType);
                return await GetSegmentResult(playlistPath, segmentPath, requestedIndex, segmentLength, job, cancellationToken).ConfigureAwait(false);
            }

            await ApiEntryPoint.Instance.TranscodingStartLock.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                if (File.Exists(segmentPath))
                {
                    job = ApiEntryPoint.Instance.OnTranscodeBeginRequest(playlistPath, TranscodingJobType);
                    return await GetSegmentResult(playlistPath, segmentPath, requestedIndex, segmentLength, job, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var currentTranscodingIndex = GetCurrentTranscodingIndex(playlistPath, segmentExtension);
                    var segmentGapRequiringTranscodingChange = 24/state.SegmentLength;
                    if (currentTranscodingIndex == null || requestedIndex < currentTranscodingIndex.Value || (requestedIndex - currentTranscodingIndex.Value) > segmentGapRequiringTranscodingChange)
                    {
                        // If the playlist doesn't already exist, startup ffmpeg
                        try
                        {
                            ApiEntryPoint.Instance.KillTranscodingJobs(request.DeviceId, request.StreamId, p => false);

                            if (currentTranscodingIndex.HasValue)
                            {
                                DeleteLastFile(playlistPath, segmentExtension, 0);
                            }

                            request.StartTimeTicks = GetSeekPositionTicks(state, requestedIndex);

                            job = await StartFfMpeg(state, playlistPath, cancellationTokenSource).ConfigureAwait(false);
                            ApiEntryPoint.Instance.OnTranscodeBeginRequest(job);
                        }
                        catch
                        {
                            state.Dispose();
                            throw;
                        }

                        await WaitForMinimumSegmentCount(playlistPath, 1, cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                ApiEntryPoint.Instance.TranscodingStartLock.Release();
            }

            Logger.Info("waiting for {0}", segmentPath);
            while (!File.Exists(segmentPath))
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            Logger.Info("returning {0}", segmentPath);
            job = job ?? ApiEntryPoint.Instance.OnTranscodeBeginRequest(playlistPath, TranscodingJobType);
            return await GetSegmentResult(playlistPath, segmentPath, requestedIndex, segmentLength, job, cancellationToken).ConfigureAwait(false);
        }

        private long GetSeekPositionTicks(StreamState state, int requestedIndex)
        {
            var startSeconds = requestedIndex * state.SegmentLength;
            var position = TimeSpan.FromSeconds(startSeconds).Ticks;

            return position;
        }

        public int? GetCurrentTranscodingIndex(string playlist, string segmentExtension)
        {
            var job = ApiEntryPoint.Instance.GetTranscodingJob(playlist, TranscodingJobType);

            if (job == null || job.HasExited)
            {
                return null;
            }

            var file = GetLastTranscodingFile(playlist, segmentExtension, FileSystem);

            if (file == null)
            {
                return null;
            }

            var playlistFilename = Path.GetFileNameWithoutExtension(playlist);

            var indexString = Path.GetFileNameWithoutExtension(file.Name).Substring(playlistFilename.Length);

            return int.Parse(indexString, NumberStyles.Integer, UsCulture);
        }

        private void DeleteLastFile(string playlistPath, string segmentExtension, int retryCount)
        {
            var file = GetLastTranscodingFile(playlistPath, segmentExtension, FileSystem);

            if (file != null)
            {
                DeleteFile(file, retryCount);
            }
        }

        private void DeleteFile(FileInfo file, int retryCount)
        {
            if (retryCount >= 5)
            {
                return;
            }

            try
            {
                FileSystem.DeleteFile(file.FullName);
            }
            catch (IOException ex)
            {
                Logger.ErrorException("Error deleting partial stream file(s) {0}", ex, file.FullName);

                Thread.Sleep(100);
                DeleteFile(file, retryCount + 1);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error deleting partial stream file(s) {0}", ex, file.FullName);
            }
        }

        private static FileInfo GetLastTranscodingFile(string playlist, string segmentExtension, IFileSystem fileSystem)
        {
            var folder = Path.GetDirectoryName(playlist);

            var filePrefix = Path.GetFileNameWithoutExtension(playlist) ?? string.Empty;

            try
            {
                return new DirectoryInfo(folder)
                    .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(i => string.Equals(i.Extension, segmentExtension, StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(i.Name).StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(fileSystem.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        protected override int GetStartNumber(StreamState state)
        {
            return GetStartNumber(state.VideoRequest);
        }

        private int GetStartNumber(VideoStreamRequest request)
        {
            var segmentId = "0";

            var segmentRequest = request as GetDynamicHlsVideoSegment;
            if (segmentRequest != null)
            {
                segmentId = segmentRequest.SegmentId;
            }

            return int.Parse(segmentId, NumberStyles.Integer, UsCulture);
        }

        private string GetSegmentPath(string playlist, int index)
        {
            var folder = Path.GetDirectoryName(playlist);

            var filename = Path.GetFileNameWithoutExtension(playlist);

            return Path.Combine(folder, filename + index.ToString(UsCulture) + ".ts");
        }

        private async Task<object> GetSegmentResult(string playlistPath,
            string segmentPath,
            int segmentIndex,
            int segmentLength,
            TranscodingJob transcodingJob,
            CancellationToken cancellationToken)
        {
            // If all transcoding has completed, just return immediately
            if (transcodingJob != null && transcodingJob.HasExited)
            {
                return GetSegmentResult(segmentPath, segmentIndex, segmentLength, transcodingJob);
            }

            var segmentFilename = Path.GetFileName(segmentPath);

            using (var fileStream = FileSystem.GetFileStream(playlistPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, true))
            {
                using (var reader = new StreamReader(fileStream))
                {
                    while (!reader.EndOfStream)
                    {
                        var text = await reader.ReadLineAsync().ConfigureAwait(false);

                        // If it appears in the playlist, it's done
                        if (text.IndexOf(segmentFilename, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            return GetSegmentResult(segmentPath, segmentIndex, segmentLength, transcodingJob);
                        }
                    }
                }
            }

            // if a different file is encoding, it's done
            //var currentTranscodingIndex = GetCurrentTranscodingIndex(playlistPath);
            //if (currentTranscodingIndex > segmentIndex)
            //{
            //return GetSegmentResult(segmentPath, segmentIndex);
            //}

            // Wait for the file to stop being written to, then stream it
            var length = new FileInfo(segmentPath).Length;
            var eofCount = 0;

            while (eofCount < 10)
            {
                var info = new FileInfo(segmentPath);

                if (!info.Exists)
                {
                    break;
                }

                var newLength = info.Length;

                if (newLength == length)
                {
                    eofCount++;
                }
                else
                {
                    eofCount = 0;
                }

                length = newLength;
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            return GetSegmentResult(segmentPath, segmentIndex, segmentLength, transcodingJob);
        }

        private object GetSegmentResult(string segmentPath, int index, int segmentLength, TranscodingJob transcodingJob)
        {
            var segmentEndingSeconds = (1 + index) * segmentLength;
            var segmentEndingPositionTicks = TimeSpan.FromSeconds(segmentEndingSeconds).Ticks;

            return ResultFactory.GetStaticFileResult(Request, new StaticFileResultOptions
            {
                Path = segmentPath,
                FileShare = FileShare.ReadWrite,
                OnComplete = () =>
                {
                    if (transcodingJob != null)
                    {
                        transcodingJob.DownloadPositionTicks = Math.Max(transcodingJob.DownloadPositionTicks ?? segmentEndingPositionTicks, segmentEndingPositionTicks);
                        ApiEntryPoint.Instance.OnTranscodeEndRequest(transcodingJob);
                    }
                }
            });
        }

        private async Task<object> GetAsync(GetMasterHlsVideoStream request, string method)
        {
            var state = await GetState(request, CancellationToken.None).ConfigureAwait(false);

            if (string.IsNullOrEmpty(request.MediaSourceId))
            {
                throw new ArgumentException("MediaSourceId is required");
            }

            var playlistText = string.Empty;

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var audioBitrate = state.OutputAudioBitrate ?? 0;
                var videoBitrate = state.OutputVideoBitrate ?? 0;

                playlistText = GetMasterPlaylistFileText(state, videoBitrate + audioBitrate);
            }

            return ResultFactory.GetResult(playlistText, MimeTypes.GetMimeType("playlist.m3u8"), new Dictionary<string, string>());
        }

        private string GetMasterPlaylistFileText(StreamState state, int totalBitrate)
        {
            var builder = new StringBuilder();

            builder.AppendLine("#EXTM3U");

            var queryStringIndex = Request.RawUrl.IndexOf('?');
            var queryString = queryStringIndex == -1 ? string.Empty : Request.RawUrl.Substring(queryStringIndex);

            var isLiveStream = (state.RunTimeTicks ?? 0) == 0;

            // Main stream
            var playlistUrl = isLiveStream ? "live.m3u8" : "main.m3u8";
            playlistUrl += queryString;

            var request = (GetMasterHlsVideoStream)state.Request;

            var subtitleStreams = state.AllMediaStreams
                .Where(i => i.IsTextSubtitleStream)
                .ToList();

            var subtitleGroup = subtitleStreams.Count > 0 && request.SubtitleMethod == SubtitleDeliveryMethod.Hls ?
                "subs" :
                null;

            AppendPlaylist(builder, playlistUrl, totalBitrate, subtitleGroup);

            if (EnableAdaptiveBitrateStreaming(state, isLiveStream))
            {
                var requestedVideoBitrate = state.VideoRequest.VideoBitRate.Value;

                // By default, vary by just 200k
                var variation = GetBitrateVariation(totalBitrate);

                var newBitrate = totalBitrate - variation;
                var variantUrl = ReplaceBitrate(playlistUrl, requestedVideoBitrate, (requestedVideoBitrate - variation));
                AppendPlaylist(builder, variantUrl, newBitrate, subtitleGroup);

                variation *= 2;
                newBitrate = totalBitrate - variation;
                variantUrl = ReplaceBitrate(playlistUrl, requestedVideoBitrate, (requestedVideoBitrate - variation));
                AppendPlaylist(builder, variantUrl, newBitrate, subtitleGroup);
            }

            if (!string.IsNullOrWhiteSpace(subtitleGroup))
            {
                AddSubtitles(state, subtitleStreams, builder);
            }

            return builder.ToString();
        }

        private string ReplaceBitrate(string url, int oldValue, int newValue)
        {
            return url.Replace(
                "videobitrate=" + oldValue.ToString(UsCulture),
                "videobitrate=" + newValue.ToString(UsCulture),
                StringComparison.OrdinalIgnoreCase);
        }

        private void AddSubtitles(StreamState state, IEnumerable<MediaStream> subtitles, StringBuilder builder)
        {
            var selectedIndex = state.SubtitleStream == null ? (int?)null : state.SubtitleStream.Index;

            foreach (var stream in subtitles)
            {
                const string format = "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"{0}\",DEFAULT={1},FORCED={2},URI=\"{3}\",LANGUAGE=\"{4}\"";

                var name = stream.Language;

                var isDefault = selectedIndex.HasValue && selectedIndex.Value == stream.Index;
                var isForced = stream.IsForced;

                if (string.IsNullOrWhiteSpace(name)) name = stream.Codec ?? "Unknown";

                var url = string.Format("{0}/Subtitles/{1}/subtitles.m3u8?SegmentLength={2}",
                    state.Request.MediaSourceId,
                    stream.Index.ToString(UsCulture),
                    30.ToString(UsCulture));

                var line = string.Format(format,
                    name,
                    isDefault ? "YES" : "NO",
                    isForced ? "YES" : "NO",
                    url,
                    stream.Language ?? "Unknown");

                builder.AppendLine(line);
            }
        }

        private bool EnableAdaptiveBitrateStreaming(StreamState state, bool isLiveStream)
        {
            // Within the local network this will likely do more harm than good.
            if (Request.IsLocal || NetworkManager.IsInLocalNetwork(Request.RemoteIp))
            {
                return false;
            }

            var request = state.Request as GetMasterHlsVideoStream;
            if (request != null && !request.EnableAdaptiveBitrateStreaming)
            {
                return false;
            }

            if (isLiveStream || string.IsNullOrWhiteSpace(state.MediaPath))
            {
                // Opening live streams is so slow it's not even worth it
                return false;
            }

            if (string.Equals(state.OutputVideoCodec, "copy", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(state.OutputAudioCodec, "copy", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return state.VideoRequest.VideoBitRate.HasValue;
        }

        private void AppendPlaylist(StringBuilder builder, string url, int bitrate, string subtitleGroup)
        {
            var header = "#EXT-X-STREAM-INF:BANDWIDTH=" + bitrate.ToString(UsCulture);

            if (!string.IsNullOrWhiteSpace(subtitleGroup))
            {
                header += string.Format(",SUBTITLES=\"{0}\"", subtitleGroup);
            }

            builder.AppendLine(header);
            builder.AppendLine(url);
        }

        private int GetBitrateVariation(int bitrate)
        {
            // By default, vary by just 50k
            var variation = 50000;

            if (bitrate >= 10000000)
            {
                variation = 2000000;
            }
            else if (bitrate >= 5000000)
            {
                variation = 1500000;
            }
            else if (bitrate >= 3000000)
            {
                variation = 1000000;
            }
            else if (bitrate >= 2000000)
            {
                variation = 500000;
            }
            else if (bitrate >= 1000000)
            {
                variation = 300000;
            }
            else if (bitrate >= 600000)
            {
                variation = 200000;
            }
            else if (bitrate >= 400000)
            {
                variation = 100000;
            }

            return variation;
        }

        private async Task<object> GetPlaylistAsync(VideoStreamRequest request, string name)
        {
            var state = await GetState(request, CancellationToken.None).ConfigureAwait(false);

            var builder = new StringBuilder();

            builder.AppendLine("#EXTM3U");
            builder.AppendLine("#EXT-X-VERSION:3");
            builder.AppendLine("#EXT-X-TARGETDURATION:" + state.SegmentLength.ToString(UsCulture));
            builder.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
            builder.AppendLine("#EXT-X-ALLOW-CACHE:NO");

            var queryStringIndex = Request.RawUrl.IndexOf('?');
            var queryString = queryStringIndex == -1 ? string.Empty : Request.RawUrl.Substring(queryStringIndex);

            var seconds = TimeSpan.FromTicks(state.RunTimeTicks ?? 0).TotalSeconds;

            var index = 0;

            while (seconds > 0)
            {
                var length = seconds >= state.SegmentLength ? state.SegmentLength : seconds;

                builder.AppendLine("#EXTINF:" + length.ToString(UsCulture) + ",");

                builder.AppendLine(string.Format("hlsdynamic/{0}/{1}.ts{2}",

                    name,
                    index.ToString(UsCulture),
                    queryString));

                seconds -= state.SegmentLength;
                index++;
            }

            builder.AppendLine("#EXT-X-ENDLIST");

            var playlistText = builder.ToString();

            return ResultFactory.GetResult(playlistText, MimeTypes.GetMimeType("playlist.m3u8"), new Dictionary<string, string>());
        }

        protected override string GetAudioArguments(StreamState state)
        {
            var codec = state.OutputAudioCodec;

            if (string.Equals(codec, "copy", StringComparison.OrdinalIgnoreCase))
            {
                return "-codec:a:0 copy";
            }

            var args = "-codec:a:0 " + codec;

            var channels = state.OutputAudioChannels;

            if (channels.HasValue)
            {
                args += " -ac " + channels.Value;
            }

            var bitrate = state.OutputAudioBitrate;

            if (bitrate.HasValue)
            {
                args += " -ab " + bitrate.Value.ToString(UsCulture);
            }

            args += " " + GetAudioFilterParam(state, true);

            return args;
        }

        protected override string GetVideoArguments(StreamState state)
        {
            var codec = state.OutputVideoCodec;

            var args = "-codec:v:0 " + codec;

            if (state.EnableMpegtsM2TsMode)
            {
                args += " -mpegts_m2ts_mode 1";
            }

            // See if we can save come cpu cycles by avoiding encoding
            if (codec.Equals("copy", StringComparison.OrdinalIgnoreCase))
            {
                return state.VideoStream != null && IsH264(state.VideoStream) ?
                    args + " -bsf:v h264_mp4toannexb" :
                    args;
            }

            var keyFrameArg = string.Format(" -force_key_frames expr:gte(t,n_forced*{0})",
                state.SegmentLength.ToString(UsCulture));

            var hasGraphicalSubs = state.SubtitleStream != null && !state.SubtitleStream.IsTextSubtitleStream;

            args += " " + GetVideoQualityParam(state, H264Encoder, true) + keyFrameArg;

            // Add resolution params, if specified
            if (!hasGraphicalSubs)
            {
                args += GetOutputSizeParam(state, codec, false);
            }

            // This is for internal graphical subs
            if (hasGraphicalSubs)
            {
                args += GetGraphicalSubtitleParam(state, codec);
            }

            return args;
        }

        protected override string GetCommandLineArguments(string outputPath, string transcodingJobId, StreamState state, bool isEncoding)
        {
            var threads = GetNumberOfThreads(state, false);

            var inputModifier = GetInputModifier(state);

            // If isEncoding is true we're actually starting ffmpeg
            var startNumberParam = isEncoding ? GetStartNumber(state).ToString(UsCulture) : "0";

            if (state.EnableGenericHlsSegmenter)
            {
                var outputTsArg = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath)) + "%d.ts";

                return string.Format("{0} {1} -map_metadata -1 -threads {2} {3} {4} -copyts -flags -global_header {5} -f segment -segment_time {6} -segment_start_number {7} -segment_list \"{8}\" -y \"{9}\"",
                    inputModifier,
                    GetInputArgument(transcodingJobId, state),
                    threads,
                    GetMapArgs(state),
                    GetVideoArguments(state),
                    GetAudioArguments(state),
                    state.SegmentLength.ToString(UsCulture),
                    startNumberParam,
                    outputPath,
                    outputTsArg
                    ).Trim();
            }

            return string.Format("{0} {1} -map_metadata -1 -threads {2} {3} {4} -copyts -flags -global_header {5} -hls_time {6} -start_number {7} -hls_list_size {8} -y \"{9}\"",
                            inputModifier,
                            GetInputArgument(transcodingJobId, state),
                            threads,
                            GetMapArgs(state),
                            GetVideoArguments(state),
                            GetAudioArguments(state),
                            state.SegmentLength.ToString(UsCulture),
                            startNumberParam,
                            state.HlsListSize.ToString(UsCulture),
                            outputPath
                            ).Trim();
        }

        /// <summary>
        /// Gets the segment file extension.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>System.String.</returns>
        protected override string GetSegmentFileExtension(StreamState state)
        {
            return ".ts";
        }
    }
}
