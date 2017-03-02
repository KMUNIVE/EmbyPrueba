using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Net;

namespace Emby.Server.Implementations.LiveTv.TunerHosts.HdHomerun
{
    /// @todo This should probably be moved elsewhere
    public static class AsyncExtensions
    {
        public static async Task<T> WithCancellation<T>( this Task<T> task, CancellationToken cancellationToken )
        {
            var tcs = new TaskCompletionSource<bool>();
            using( cancellationToken.Register( s => ( (TaskCompletionSource<bool>)s ).TrySetResult( true ), tcs ) )
            {
                if( task != await Task.WhenAny( task, tcs.Task ) )
                {
                    throw new OperationCanceledException( cancellationToken );
                }
            }

            return await task;
        }
    }

    public class LegacyHdHomerunLiveStream : LiveStream, IDirectStreamProvider
    {
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IServerApplicationPaths _appPaths;
        private readonly IServerApplicationHost _appHost;
        private readonly ISocketFactory _socketFactory;

        private readonly CancellationTokenSource _liveStreamCancellationTokenSource = new CancellationTokenSource();
        private readonly TaskCompletionSource<bool> _liveStreamTaskCompletionSource = new TaskCompletionSource<bool>();
        private readonly MulticastStream _multicastStream;
        private readonly string _channelUrl;
        private readonly int _numTuners;

        public LegacyHdHomerunLiveStream(MediaSourceInfo mediaSource, string originalStreamId, string channelUrl, int numTuners, IFileSystem fileSystem, IHttpClient httpClient, ILogger logger, IServerApplicationPaths appPaths, IServerApplicationHost appHost, ISocketFactory socketFactory)
            : base(mediaSource)
        {
            _fileSystem = fileSystem;
            _httpClient = httpClient;
            _logger = logger;
            _appPaths = appPaths;
            _appHost = appHost;
            _socketFactory = socketFactory;
            OriginalStreamId = originalStreamId;
            _multicastStream = new MulticastStream(_logger);
            _channelUrl = channelUrl;
            _numTuners = numTuners;
        }

        protected override async Task OpenInternal(CancellationToken openCancellationToken)
        {
            _liveStreamCancellationTokenSource.Token.ThrowIfCancellationRequested();

            var mediaSource = OriginalMediaSource;

            var splitString = mediaSource.Path.Split('_');
            var remoteIp = splitString[0];
            var localPort = Convert.ToInt32(splitString[1]);

            _logger.Info("Opening Legacy HDHR Live stream from {0}", remoteIp);

            var taskCompletionSource = new TaskCompletionSource<bool>();

            StartStreaming(remoteIp, localPort, taskCompletionSource, _liveStreamCancellationTokenSource.Token);

            //OpenedMediaSource.Protocol = MediaProtocol.File;
            //OpenedMediaSource.Path = tempFile;
            //OpenedMediaSource.ReadAtNativeFramerate = true;

            OpenedMediaSource.Path = _appHost.GetLocalApiUrl("127.0.0.1") + "/LiveTv/LiveStreamFiles/" + UniqueId + "/stream.ts";
            OpenedMediaSource.Protocol = MediaProtocol.Http;
            OpenedMediaSource.SupportsDirectPlay = false;
            OpenedMediaSource.SupportsDirectStream = true;
            OpenedMediaSource.SupportsTranscoding = true;

            await taskCompletionSource.Task.ConfigureAwait(false);

            //await Task.Delay(5000).ConfigureAwait(false);
        }

        public override Task Close()
        {
            _logger.Info("Closing Legacy HDHR live stream");
            _liveStreamCancellationTokenSource.Cancel();

            return _liveStreamTaskCompletionSource.Task;
        }

        private async Task StartStreaming(string remoteIp, int localPort, TaskCompletionSource<bool> openTaskCompletionSource, CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                var isFirstAttempt = true;
                var udpClient = _socketFactory.CreateUdpSocket(localPort);
                LegacyHdHomerunCommand legCommand = new LegacyHdHomerunCommand(_socketFactory);

                var remoteAddress = new IpAddressInfo(remoteIp, IpAddressFamily.InterNetwork);
                IpAddressInfo localAddress = null;
                var tcpSocket = _socketFactory.CreateSocket(IpAddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, false);
                try
                {
                    tcpSocket.Connect(new IpEndPointInfo(remoteAddress, LegacyHdHomerunCommand.HdHomeRunPort));
                    localAddress = tcpSocket.LocalEndPoint.IpAddress;
                    tcpSocket.Close();
                }
                catch (Exception)
                {
                    _logger.Error("Unable to determine local ip address for Legacy HDHomerun stream.");
                    return;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // send url to start streaming
                        await legCommand.StartStreaming(remoteAddress, localAddress, localPort, _channelUrl, _numTuners, cancellationToken).ConfigureAwait(false);

                        var response = await udpClient.ReceiveAsync().WithCancellation(cancellationToken);
                        _logger.Info("Opened Legacy HDHR stream from {0}", _channelUrl);

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Action onStarted = null;
                            if (isFirstAttempt)
                            {
                                onStarted = () => openTaskCompletionSource.TrySetResult(true);
                            }
                                
                            var stream = new UdpClientStream(udpClient);
                            await _multicastStream.CopyUntilCancelled(stream, onStarted, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (isFirstAttempt)
                        {
                            _logger.ErrorException("Error opening live stream:", ex);
                            openTaskCompletionSource.TrySetException(ex);
                            break;
                        }

                        _logger.ErrorException("Error copying live stream, will reopen", ex);
                    }

                    isFirstAttempt = false;
                }


                legCommand.StopStreaming();
                udpClient.Dispose();
                _liveStreamTaskCompletionSource.TrySetResult(true);

            }).ConfigureAwait(false);
        }

        public Task CopyToAsync(Stream stream, CancellationToken cancellationToken)
        {
            return _multicastStream.CopyToAsync(stream);
        }
    }

    // This handles the ReadAsync function only of a Stream object
    // This is used to wrap a UDP socket into a stream for MulticastStream which only uses ReadAsync
    public class UdpClientStream : Stream
    {
        private static int RtpHeaderBytes = 12;
        private static int PacketSize = 1316;
        private readonly IUdpSocket _udpClient;
        bool disposed;

        public UdpClientStream(IUdpSocket udpClient) : base()
        {
            _udpClient = udpClient;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset + count < 0)
                throw new ArgumentOutOfRangeException("offset + count must not be negative", "offset+count");

            if (offset + count > buffer.Length)
                throw new ArgumentException("offset + count must not be greater than the length of buffer", "offset+count");

            if (disposed)
                throw new ObjectDisposedException(typeof(UdpClientStream).ToString());

            // This will always receive a 1328 packet size (PacketSize + RtpHeaderSize)
            // The RTP header will be stripped so see how many reads we need to make to fill the buffer.
            int numReads = count / PacketSize;
            int totalBytesRead = 0;

            for (int i = 0; i < numReads; ++i)
            {
                var data = await _udpClient.ReceiveAsync().WithCancellation(cancellationToken);

                var bytesRead = data.ReceivedBytes - RtpHeaderBytes;

                // remove rtp header
                Buffer.BlockCopy(data.Buffer, RtpHeaderBytes, buffer, offset, bytesRead);
                offset += bytesRead;
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }

        protected override void Dispose(bool disposing)
        {
            disposed = true;
        }

        public override bool CanRead
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool CanSeek
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool CanWrite
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
