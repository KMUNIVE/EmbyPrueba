namespace MediaBrowser.Model.Notifications
{
    public enum NotificationType
    {
        ApplicationUpdateAvailable,
        ApplicationUpdateInstalled,
        AudioPlayback,
        GamePlayback,
        VideoPlayback,
        AudioPlaybackStopped,
        GamePlaybackStopped,
        VideoPlaybackStopped,
        InstallationFailed,
        PluginError,
        PluginInstalled,
        PluginUpdateInstalled,
        PluginUninstalled,
        NewLibraryContent,
        NewLibraryContentMultiple,
        ServerRestartRequired,
        TaskFailed,
        CameraImageUploaded,
        UserLockedOut
    }
}