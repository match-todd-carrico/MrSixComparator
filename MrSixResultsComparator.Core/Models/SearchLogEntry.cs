namespace MrSixResultsComparator.Core.Models;

public class SearchLogEntry
{
    public DateTime CallTime { get; set; }
    public Guid CallID { get; set; }
    public Guid SID { get; set; }
    public short SiteCode { get; set; }
    public short? URLCode { get; set; }
    public byte? PlatformID { get; set; }
    public string Searchname { get; set; } = string.Empty;
    public string Servername { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public int SearcherUserID { get; set; }
    public string Algorithm { get; set; } = string.Empty;
    public int Duration { get; set; }
    public short RequestCount { get; set; }
    public int ReturnedCount { get; set; }
    public int AvailableMatches { get; set; }
    public byte SearchGeoTypeID { get; set; }
    public short? CountryCode { get; set; }
    public byte? StateCode { get; set; }
    public int? CityCode { get; set; }
    public string? CityCodes { get; set; }
    public string? PostalCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? CBSACode { get; set; }
    public short? Distance { get; set; }
    public byte? GenderGenderSeek { get; set; }
    public byte? Age { get; set; }
    public byte? LAge { get; set; }
    public byte? UAge { get; set; }
    public short? Height { get; set; }
    public short? LHeight { get; set; }
    public short? UHeight { get; set; }
    public int? OtherUserId { get; set; }
    public bool? ServedByCommonality { get; set; }
    public string? SeekString { get; set; }
    public string? SelfString { get; set; }
    public string? WeightString { get; set; }
    public string? KeyWord { get; set; }
    public bool UseDefaultDistance { get; set; }
    public string SearchCriteriaOptions { get; set; } = string.Empty;
    public int? IMOnlyMs { get; set; }
    public bool SearchBlock { get; set; }
    public bool PhotosOnly { get; set; }
    public bool? OnlineNow { get; set; }
    public bool SpotlightOnly { get; set; }
    public bool SubscriberOnly { get; set; }
    public bool CertOnly { get; set; }
    public string? ParamBag { get; set; }
    public int? WhatIfSearchId { get; set; }
    public string? ResultBag { get; set; }
}
