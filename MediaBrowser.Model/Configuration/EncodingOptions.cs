﻿
namespace MediaBrowser.Model.Configuration
{
    public class EncodingOptions
    {
        public int EncodingThreadCount { get; set; }
        public string TranscodingTempPath { get; set; }
        public double DownMixAudioBoost { get; set; }
        public string H264Encoder { get; set; }
        public bool EnableDebugLogging { get; set; }
        public bool EnableThrottling { get; set; }
        public int ThrottleThresholdInSeconds { get; set; }
        public bool SupportQsvDecoding { get; set; } //TODO: use a utility to detect if the server is configured to support QSV

        public EncodingOptions()
        {
            H264Encoder = "libx264";
            DownMixAudioBoost = 2;
            EnableThrottling = true;
            ThrottleThresholdInSeconds = 120;
            EncodingThreadCount = -1;
            SupportQsvDecoding = false;
        }
    }
}
