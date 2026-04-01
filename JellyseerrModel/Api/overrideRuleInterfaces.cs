using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;

namespace Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel.Api;

public class OverrideRuleResultsResponse
{
    public List<OverrideRule> Value { get; set; } = new();

}

