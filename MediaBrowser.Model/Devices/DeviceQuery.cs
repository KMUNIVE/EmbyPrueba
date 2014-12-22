﻿
namespace MediaBrowser.Model.Devices
{
    public class DeviceQuery
    {
        /// <summary>
        /// Gets or sets a value indicating whether [supports content uploading].
        /// </summary>
        /// <value><c>null</c> if [supports content uploading] contains no value, <c>true</c> if [supports content uploading]; otherwise, <c>false</c>.</value>
        public bool? SupportsContentUploading { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether [supports unique identifier].
        /// </summary>
        /// <value><c>null</c> if [supports unique identifier] contains no value, <c>true</c> if [supports unique identifier]; otherwise, <c>false</c>.</value>
        public bool? SupportsUniqueIdentifier { get; set; }
    }
}
