namespace MediaBrowser.Controller.Providers
{
    public enum ImageRefreshMode
    {
        /// <summary>
        /// The none
        /// </summary>
        None = 0,

        /// <summary>
        /// The default
        /// </summary>
        Default = 1,

        /// <summary>
        /// Existing images will be validated
        /// </summary>
        ValidationOnly = 2,

        /// <summary>
        /// All providers will be executed to search for new metadata
        /// </summary>
        FullRefresh = 3
    }
}