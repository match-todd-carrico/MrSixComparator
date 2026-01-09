using MrSIXProxyV2.Input;

namespace MrSixResultsComparator.Core.Models;

public class SearchParameter
{
    public string Description { get; set; } = string.Empty;
    public string Criteria { get; set; } = string.Empty;
    public short SiteCode { get; set; }
    public int ShardId { get; set; }
    public int SearcherUserId { get; set; }
    public short RequestCount { get; set; }
    public int WhatIfSearchId { get; set; }
    public GeoCriteria? Geo { get; set; }
    public Guid CallId { get; set; }
    public DateTime CallTime { get; set; }
}
