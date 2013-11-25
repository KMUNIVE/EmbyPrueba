﻿using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Querying;
using ServiceStack.ServiceHost;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Api.LiveTv
{
    [Route("/LiveTv/Services", "GET")]
    [Api(Description = "Gets available live tv services.")]
    public class GetServices : IReturn<List<LiveTvServiceInfo>>
    {
        [ApiMember(Name = "ServiceName", Description = "Optional filter by service.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string ServiceName { get; set; }
    }

    [Route("/LiveTv/Channels", "GET")]
    [Api(Description = "Gets available live tv channels.")]
    public class GetChannels : IReturn<QueryResult<ChannelInfoDto>>
    {
        [ApiMember(Name = "ServiceName", Description = "Optional filter by service.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string ServiceName { get; set; }

        [ApiMember(Name = "Type", Description = "Optional filter by channel type.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public ChannelType? Type { get; set; }

        [ApiMember(Name = "UserId", Description = "Optional filter by channel user id.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string UserId { get; set; }
    }

    [Route("/LiveTv/Channels/{Id}", "GET")]
    [Api(Description = "Gets a live tv channel")]
    public class GetChannel : IReturn<ChannelInfoDto>
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        [ApiMember(Name = "Id", Description = "Channel Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string Id { get; set; }
    }

    [Route("/LiveTv/Programs", "GET")]
    [Api(Description = "Gets available live tv epgs..")]
    public class GetPrograms : IReturn<QueryResult<ProgramInfoDto>>
    {
        [ApiMember(Name = "ServiceName", Description = "Live tv service name", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string ServiceName { get; set; }

        [ApiMember(Name = "ChannelIds", Description = "The channels to return guide information for.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string ChannelIds { get; set; }
    }

    public class LiveTvService : BaseApiService
    {
        private readonly ILiveTvManager _liveTvManager;

        public LiveTvService(ILiveTvManager liveTvManager)
        {
            _liveTvManager = liveTvManager;
        }

        private IEnumerable<ILiveTvService> GetServices(string serviceName)
        {
            IEnumerable<ILiveTvService> services = _liveTvManager.Services;

            if (!string.IsNullOrEmpty(serviceName))
            {
                services = services.Where(i => string.Equals(i.Name, serviceName, System.StringComparison.OrdinalIgnoreCase));
            }

            return services;
        }

        public object Get(GetServices request)
        {
            var services = GetServices(request.ServiceName)
                .Select(GetServiceInfo)
                .ToList();

            return ToOptimizedResult(services);
        }

        private LiveTvServiceInfo GetServiceInfo(ILiveTvService service)
        {
            return new LiveTvServiceInfo
            {
                Name = service.Name
            };
        }

        public object Get(GetChannels request)
        {
            var result = _liveTvManager.GetChannels(new ChannelQuery
            {
                ChannelType = request.Type,
                ServiceName = request.ServiceName,
                UserId = request.UserId

            });

            return ToOptimizedResult(result);
        }

        public object Get(GetChannel request)
        {
            var result = _liveTvManager.GetChannel(request.Id);

            return ToOptimizedResult(_liveTvManager.GetChannelInfoDto(result));
        }

        public object Get(GetPrograms request)
        {
            var result = _liveTvManager.GetPrograms(new ProgramQuery
            {
                ServiceName = request.ServiceName,
                ChannelIdList = (request.ChannelIds ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToArray()
            });

            return ToOptimizedResult(result);
        }
    }
}