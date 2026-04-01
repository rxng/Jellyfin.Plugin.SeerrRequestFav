using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;

using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel.Server;
namespace Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel.Api;

public class PersonCombinedCreditsResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("cast")]
    public List<PersonCreditCast> Cast { get; set; } = new();

    [JsonPropertyName("crew")]
    public List<PersonCreditCrew> Crew { get; set; } = new();

}


