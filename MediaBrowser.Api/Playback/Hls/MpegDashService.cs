﻿using MediaBrowser.Common.IO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Diagnostics;
using MediaBrowser.Controller.Dlna;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.IO;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MimeTypes = MediaBrowser.Model.Net.MimeTypes;

namespace MediaBrowser.Api.Playback.Hls
{
    /// <summary>
    /// Options is needed for chromecast. Threw Head in there since it's related
    /// </summary>
    [Route("/Videos/{Id}/master.mpd", "GET", Summary = "Gets a video stream using Mpeg dash.")]
    [Route("/Videos/{Id}/master.mpd", "HEAD", Summary = "Gets a video stream using Mpeg dash.")]
    public class GetMasterManifest : VideoStreamRequest
    {
        public bool EnableAdaptiveBitrateStreaming { get; set; }

        public GetMasterManifest()
        {
            EnableAdaptiveBitrateStreaming = true;
        }
    }

    [Route("/Videos/{Id}/dash/{SegmentType}/{SegmentId}.m4s", "GET")]
    public class GetDashSegment : VideoStreamRequest
    {
        /// <summary>
        /// Gets or sets the segment id.
        /// </summary>
        /// <value>The segment id.</value>
        public string SegmentId { get; set; }

        /// <summary>
        /// Gets or sets the type of the segment.
        /// </summary>
        /// <value>The type of the segment.</value>
        public string SegmentType { get; set; }
    }

    public class MpegDashService : BaseHlsService
    {
        public MpegDashService(IServerConfigurationManager serverConfig, IUserManager userManager, ILibraryManager libraryManager, IIsoManager isoManager, IMediaEncoder mediaEncoder, IFileSystem fileSystem, ILiveTvManager liveTvManager, IDlnaManager dlnaManager, ISubtitleEncoder subtitleEncoder, IDeviceManager deviceManager, IProcessManager processManager, IMediaSourceManager mediaSourceManager, INetworkManager networkManager)
            : base(serverConfig, userManager, libraryManager, isoManager, mediaEncoder, fileSystem, liveTvManager, dlnaManager, subtitleEncoder, deviceManager, processManager, mediaSourceManager)
        {
            NetworkManager = networkManager;
        }

        protected INetworkManager NetworkManager { get; private set; }

        public object Get(GetMasterManifest request)
        {
            var result = GetAsync(request, "GET").Result;

            return result;
        }

        public object Head(GetMasterManifest request)
        {
            var result = GetAsync(request, "HEAD").Result;

            return result;
        }

        protected override bool EnableOutputInSubFolder
        {
            get
            {
                return true;
            }
        }

        private async Task<object> GetAsync(GetMasterManifest request, string method)
        {
            if (string.Equals(request.AudioCodec, "copy", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Audio codec copy is not allowed here.");
            }

            if (string.Equals(request.VideoCodec, "copy", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Video codec copy is not allowed here.");
            }

            if (string.IsNullOrEmpty(request.MediaSourceId))
            {
                throw new ArgumentException("MediaSourceId is required");
            }

            var state = await GetState(request, CancellationToken.None).ConfigureAwait(false);

            var playlistText = string.Empty;

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                playlistText = GetManifestText(state);
            }

            return ResultFactory.GetResult(playlistText, MimeTypes.GetMimeType("playlist.mpd"), new Dictionary<string, string>());
        }

        private string GetManifestText(StreamState state)
        {
            var builder = new StringBuilder();

            var time = TimeSpan.FromTicks(state.RunTimeTicks.Value);

            var duration = "PT" + time.Hours.ToString("00", UsCulture) + "H" + time.Minutes.ToString("00", UsCulture) + "M" + time.Seconds.ToString("00", UsCulture) + ".00S";

            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");

            builder.AppendFormat(
                "<MPD xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"urn:mpeg:dash:schema:mpd:2011\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xsi:schemaLocation=\"urn:mpeg:DASH:schema:MPD:2011 http://standards.iso.org/ittf/PubliclyAvailableStandards/MPEG-DASH_schema_files/DASH-MPD.xsd\" profiles=\"urn:mpeg:dash:profile:isoff-live:2011\" type=\"static\" mediaPresentationDuration=\"{0}\" minBufferTime=\"PT5.0S\">",
                duration);

            builder.Append("<ProgramInformation>");
            builder.Append("</ProgramInformation>");

            builder.Append("<Period start=\"PT0S\">");
            builder.Append(GetVideoAdaptationSet(state));
            builder.Append(GetAudioAdaptationSet(state));
            builder.Append("</Period>");

            builder.Append("</MPD>");

            return builder.ToString();
        }

        private string GetVideoAdaptationSet(StreamState state)
        {
            var builder = new StringBuilder();

            builder.Append("<AdaptationSet id=\"video\" segmentAlignment=\"true\" bitstreamSwitching=\"true\">");
            builder.Append(GetVideoRepresentationOpenElement(state));

            AppendSegmentList(state, builder, "video");

            builder.Append("</Representation>");
            builder.Append("</AdaptationSet>");

            return builder.ToString();
        }

        private string GetAudioAdaptationSet(StreamState state)
        {
            var builder = new StringBuilder();

            builder.Append("<AdaptationSet id=\"audio\" segmentAlignment=\"true\" bitstreamSwitching=\"true\">");
            builder.Append(GetAudioRepresentationOpenElement(state));

            builder.Append("<AudioChannelConfiguration schemeIdUri=\"urn:mpeg:dash:23003:3:audio_channel_configuration:2011\" value=\"6\" />");

            AppendSegmentList(state, builder, "audio");

            builder.Append("</Representation>");
            builder.Append("</AdaptationSet>");

            return builder.ToString();
        }

        private string GetVideoRepresentationOpenElement(StreamState state)
        {
            var codecs = GetVideoCodecDescriptor(state);

            var mime = "video/mp4";

            var xml = "<Representation id=\"0\" mimeType=\"" + mime + "\" codecs=\"" + codecs + "\"";

            if (state.OutputWidth.HasValue)
            {
                xml += " width=\"" + state.OutputWidth.Value.ToString(UsCulture) + "\"";
            }
            if (state.OutputHeight.HasValue)
            {
                xml += " height=\"" + state.OutputHeight.Value.ToString(UsCulture) + "\"";
            }
            if (state.OutputVideoBitrate.HasValue)
            {
                xml += " bandwidth=\"" + state.OutputVideoBitrate.Value.ToString(UsCulture) + "\"";
            }

            xml += ">";

            return xml;
        }

        private string GetAudioRepresentationOpenElement(StreamState state)
        {
            var codecs = GetAudioCodecDescriptor(state);

            var mime = "audio/mp4";

            var xml = "<Representation id=\"1\" mimeType=\"" + mime + "\" codecs=\"" + codecs + "\"";

            if (state.OutputAudioSampleRate.HasValue)
            {
                xml += " audioSamplingRate=\"" + state.OutputAudioSampleRate.Value.ToString(UsCulture) + "\"";
            }
            if (state.OutputAudioBitrate.HasValue)
            {
                xml += " bandwidth=\"" + state.OutputAudioBitrate.Value.ToString(UsCulture) + "\"";
            }

            xml += ">";

            return xml;
        }

        private string GetVideoCodecDescriptor(StreamState state)
        {
            // https://developer.apple.com/library/ios/documentation/networkinginternet/conceptual/streamingmediaguide/FrequentlyAskedQuestions/FrequentlyAskedQuestions.html
            // http://www.chipwreck.de/blog/2010/02/25/html-5-video-tag-and-attributes/

            var level = state.TargetVideoLevel ?? 0;
            var profile = state.TargetVideoProfile ?? string.Empty;

            if (profile.IndexOf("high", StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (level >= 4.1)
                {
                    return "avc1.640028";
                }

                if (level >= 4)
                {
                    return "avc1.640028";
                }

                return "avc1.64001f";
            }

            if (profile.IndexOf("main", StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (level >= 4)
                {
                    return "avc1.4d0028";
                }

                if (level >= 3.1)
                {
                    return "avc1.4d001f";
                }

                return "avc1.4d001e";
            }

            if (level >= 3.1)
            {
                return "avc1.42001f";
            }

            return "avc1.42E01E";
        }

        private string GetAudioCodecDescriptor(StreamState state)
        {
            // https://developer.apple.com/library/ios/documentation/networkinginternet/conceptual/streamingmediaguide/FrequentlyAskedQuestions/FrequentlyAskedQuestions.html

            if (string.Equals(state.OutputAudioCodec, "mp3", StringComparison.OrdinalIgnoreCase))
            {
                return "mp4a.40.34";
            }

            // AAC 5ch
            if (state.OutputAudioChannels.HasValue && state.OutputAudioChannels.Value >= 5)
            {
                return "mp4a.40.5";
            }

            // AAC 2ch
            return "mp4a.40.2";
        }

        public object Get(GetDashSegment request)
        {
            return GetDynamicSegment(request, request.SegmentId, request.SegmentType).Result;
        }

        private void AppendSegmentList(StreamState state, StringBuilder builder, string type)
        {
            var extension = GetSegmentFileExtension(state);

            var seconds = TimeSpan.FromTicks(state.RunTimeTicks ?? 0).TotalSeconds;

            var queryStringIndex = Request.RawUrl.IndexOf('?');
            var queryString = queryStringIndex == -1 ? string.Empty : Request.RawUrl.Substring(queryStringIndex);

            var index = 0;
            var duration = 1000000 * state.SegmentLength;
            builder.AppendFormat("<SegmentList timescale=\"1000000\" duration=\"{0}\" startNumber=\"1\">", duration.ToString(CultureInfo.InvariantCulture));

            while (seconds > 0)
            {
                var segmentUrl = string.Format("dash/{3}/{0}{1}{2}",
                    index.ToString(UsCulture),
                    extension,
                    SecurityElement.Escape(queryString),
                    type);

                if (index == 0)
                {
                    builder.AppendFormat("<Initialization sourceURL=\"{0}\"/>", segmentUrl);
                }
                else
                {
                    builder.AppendFormat("<SegmentURL media=\"{0}\"/>", segmentUrl);
                }

                seconds -= state.SegmentLength;
                index++;
            }
            builder.Append("</SegmentList>");
        }

        private async Task<object> GetDynamicSegment(VideoStreamRequest request, string segmentId, string segmentType)
        {
            if ((request.StartTimeTicks ?? 0) > 0)
            {
                throw new ArgumentException("StartTimeTicks is not allowed.");
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var index = int.Parse(segmentId, NumberStyles.Integer, UsCulture);

            var state = await GetState(request, cancellationToken).ConfigureAwait(false);

            var playlistPath = Path.ChangeExtension(state.OutputFilePath, ".mpd");

            var segmentExtension = GetSegmentFileExtension(state);

            var segmentPath = GetSegmentPath(playlistPath, segmentType, segmentExtension, index);
            var segmentLength = state.SegmentLength;

            TranscodingJob job = null;

            if (File.Exists(segmentPath))
            {
                job = ApiEntryPoint.Instance.GetTranscodingJob(playlistPath, TranscodingJobType);
                return await GetSegmentResult(playlistPath, segmentPath, index, segmentLength, job, cancellationToken).ConfigureAwait(false);
            }

            await ApiEntryPoint.Instance.TranscodingStartLock.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                if (File.Exists(segmentPath))
                {
                    job = ApiEntryPoint.Instance.GetTranscodingJob(playlistPath, TranscodingJobType);
                    return await GetSegmentResult(playlistPath, segmentPath, index, segmentLength, job, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var currentTranscodingIndex = GetCurrentTranscodingIndex(playlistPath, segmentExtension);

                    if (currentTranscodingIndex == null || index < currentTranscodingIndex.Value || (index - currentTranscodingIndex.Value) > 4)
                    {
                        // If the playlist doesn't already exist, startup ffmpeg
                        try
                        {
                            ApiEntryPoint.Instance.KillTranscodingJobs(j => j.Type == TranscodingJobType && string.Equals(j.DeviceId, request.DeviceId, StringComparison.OrdinalIgnoreCase), p => !string.Equals(p, playlistPath, StringComparison.OrdinalIgnoreCase));

                            if (currentTranscodingIndex.HasValue)
                            {
                                DeleteLastFile(playlistPath, segmentExtension, 0);
                            }

                            var startSeconds = index * state.SegmentLength;
                            request.StartTimeTicks = TimeSpan.FromSeconds(startSeconds).Ticks;

                            job = await StartFfMpeg(state, playlistPath, cancellationTokenSource, Path.GetDirectoryName(playlistPath)).ConfigureAwait(false);
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
            job = job ?? ApiEntryPoint.Instance.GetTranscodingJob(playlistPath, TranscodingJobType);
            return await GetSegmentResult(playlistPath, segmentPath, index, segmentLength, job, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task WaitForMinimumSegmentCount(string playlist, int segmentCount, CancellationToken cancellationToken)
        {
            var tmpPath = playlist + ".tmp";
            Logger.Debug("Waiting for {0} segments in {1}", segmentCount, playlist);
            // Double since audio and video are split
            segmentCount = segmentCount * 2;
            // Account for the initial segments
            segmentCount += 2;

            while (true)
            {
                FileStream fileStream;
                try
                {
                    fileStream = FileSystem.GetFileStream(tmpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, true);
                }
                catch (IOException)
                {
                    fileStream = FileSystem.GetFileStream(playlist, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, true);
                }
                // Need to use FileShare.ReadWrite because we're reading the file at the same time it's being written
                using (fileStream)
                {
                    using (var reader = new StreamReader(fileStream))
                    {
                        var count = 0;

                        while (!reader.EndOfStream)
                        {
                            var line = await reader.ReadLineAsync().ConfigureAwait(false);

                            if (line.IndexOf(".m4s", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                count++;
                                if (count >= segmentCount)
                                {
                                    Logger.Debug("Finished waiting for {0} segments in {1}", segmentCount, playlist);
                                    return;
                                }
                            }
                        }
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<object> GetSegmentResult(string playlistPath,
            string segmentPath,
            int segmentIndex,
            int segmentLength,
            TranscodingJob transcodingJob,
            CancellationToken cancellationToken)
        {
            // If all transcoding has completed, just return immediately
            if (!IsTranscoding(playlistPath))
            {
                return GetSegmentResult(segmentPath, segmentIndex, segmentLength, transcodingJob);
            }

            var segmentFilename = Path.GetFileName(segmentPath);

            using (var fileStream = FileSystem.GetFileStream(playlistPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, true))
            {
                using (var reader = new StreamReader(fileStream))
                {
                    var text = await reader.ReadToEndAsync().ConfigureAwait(false);

                    // If it appears in the playlist, it's done
                    if (text.IndexOf(segmentFilename, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        return GetSegmentResult(segmentPath, segmentIndex, segmentLength, transcodingJob);
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
                    }

                }
            });
        }

        private bool IsTranscoding(string playlistPath)
        {
            var job = ApiEntryPoint.Instance.GetTranscodingJob(playlistPath, TranscodingJobType);

            return job != null && !job.HasExited;
        }

        public int? GetCurrentTranscodingIndex(string playlist, string segmentExtension)
        {
            var file = GetLastTranscodingFile(playlist, segmentExtension, FileSystem);

            if (file == null)
            {
                return null;
            }

            var playlistFilename = Path.GetFileNameWithoutExtension(playlist);

            var indexString = Path.GetFileNameWithoutExtension(file.Name).Substring(playlistFilename.Length);

            return int.Parse(indexString, NumberStyles.Integer, UsCulture);
        }

        private void DeleteLastFile(string path, string segmentExtension, int retryCount)
        {
            if (retryCount >= 5)
            {
                return;
            }

            var file = GetLastTranscodingFile(path, segmentExtension, FileSystem);

            if (file != null)
            {
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch (IOException ex)
                {
                    Logger.ErrorException("Error deleting partial stream file(s) {0}", ex, file.FullName);

                    Thread.Sleep(100);
                    DeleteLastFile(path, segmentExtension, retryCount + 1);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error deleting partial stream file(s) {0}", ex, file.FullName);
                }
            }
        }

        private static FileInfo GetLastTranscodingFile(string playlist, string segmentExtension, IFileSystem fileSystem)
        {
            var folder = Path.GetDirectoryName(playlist);

            try
            {
                return new DirectoryInfo(folder)
                    .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(i => string.Equals(i.Extension, segmentExtension, StringComparison.OrdinalIgnoreCase))
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

        private string GetSegmentPath(string playlist, string segmentType, string segmentExtension, int index)
        {
            var folder = Path.GetDirectoryName(playlist);

            var id = string.Equals(segmentType, "video", StringComparison.OrdinalIgnoreCase)
                ? "0"
                : "1";

            string filename;

            if (index == 0)
            {
                filename = "init-stream" + id + segmentExtension;
            }
            else
            {
                var number = index.ToString("00000", CultureInfo.InvariantCulture);
                filename = "chunk-stream" + id + "-" + number + segmentExtension;
            }

            return Path.Combine(folder, filename);
        }

        protected override string GetAudioArguments(StreamState state)
        {
            var codec = state.OutputAudioCodec;

            if (codec.Equals("copy", StringComparison.OrdinalIgnoreCase))
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
            // test url http://192.168.1.2:8096/videos/233e8905d559a8f230db9bffd2ac9d6d/master.mpd?mediasourceid=233e8905d559a8f230db9bffd2ac9d6d&videocodec=h264&audiocodec=aac&maxwidth=1280&videobitrate=500000&audiobitrate=128000&profile=baseline&level=3
            // Good info on i-frames http://blog.streamroot.io/encode-multi-bitrate-videos-mpeg-dash-mse-based-media-players/

            var threads = GetNumberOfThreads(state, false);

            var inputModifier = GetInputModifier(state);

            var args = string.Format("{0} {1} -map_metadata -1 -threads {2} {3} {4} -copyts {5} -f dash -use_template 0 -min_seg_duration {6} -y \"{7}\"",
                inputModifier,
                GetInputArgument(transcodingJobId, state),
                threads,
                GetMapArgs(state),
                GetVideoArguments(state),
                GetAudioArguments(state),
                (state.SegmentLength * 1000000).ToString(CultureInfo.InvariantCulture),
                outputPath
                ).Trim();

            return args;
        }

        /// <summary>
        /// Gets the segment file extension.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>System.String.</returns>
        protected override string GetSegmentFileExtension(StreamState state)
        {
            return ".m4s";
        }

        protected override TranscodingJobType TranscodingJobType
        {
            get
            {
                return TranscodingJobType.Dash;
            }
        }
    }
}
