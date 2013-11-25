﻿using System;
using System.Net.Http;

namespace MediaBrowser.ServerApplication.Native
{
	/// <summary>
	/// Class HttpClientFactory
	/// </summary>
	public static class HttpClientFactory
	{
		/// <summary>
		/// Gets the HTTP client.
		/// </summary>
		/// <param name="enableHttpCompression">if set to <c>true</c> [enable HTTP compression].</param>
		/// <returns>HttpClient.</returns>
		public static HttpClient GetHttpClient(bool enableHttpCompression)
		{
			return new HttpClient()
			{
				Timeout = TimeSpan.FromSeconds(20)
			};
		}
	}
}
