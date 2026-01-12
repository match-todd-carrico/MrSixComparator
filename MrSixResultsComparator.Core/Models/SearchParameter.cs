using MrSIXProxyV2.Input;

namespace MrSixResultsComparator.Core.Models;

public class SearchParameter
{
    public string Description { get; set; } = string.Empty;
    public short SiteCode { get; set; }
    public int ShardId { get; set; }
    public int SearcherUserId { get; set; }
    public short RequestCount { get; set; }
    public int WhatIfSearchId { get; set; }
    public GeoCriteria? Geo { get; set; }
    public Guid CallId { get; set; }
    public DateTime CallTime { get; set; }

    public string SearchName { get; set; } = string.Empty;
    public int OtherUserId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public byte GenderGenderSeek { get; set; }
    public int SearchGeoTypeId { get; set; }
    public short CountryCode { get; set; }
    public byte StateCode { get; set; }
    public int CityCode { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public double Distance { get; set; }
    public byte LAge { get; set; }
    public byte UAge { get; set; }
    public short? LHeight { get; set; }
    public short? UHeight { get; set; }
    public bool PhotosOnly { get; set; }
    public string SelfString { get; set; }
    public string SeekString { get; set; }
    public string WeightString { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<string>? StackConfigs { get; set; }

    public List<int>? SelfAnswerIds
    {
        get
        {
            if (!string.IsNullOrEmpty(SelfString))
            {
                var selfAnswerIds = new List<int>();
                var parts = SelfString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (int.TryParse(part, out int answerId))
                    {
                        selfAnswerIds.Add(answerId);
                    }
                }
                return selfAnswerIds;
            }

            return null;
        }
    }

    public List<int>? SeekingAnswerIds
    {
        get
        {
            if (!string.IsNullOrEmpty(SeekString))
            {
                var seekingAnswerIds = new List<int>();
                var parts = SeekString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (int.TryParse(part, out int answerId))
                    {
                        seekingAnswerIds.Add(answerId);
                    }
                }
                return seekingAnswerIds;
            }

            return null;
        }
    }

    public List<AttributeWeight> ? SeekingAttributeWeights
    {
        get
        {
            if (!string.IsNullOrEmpty(WeightString))
            {
                var attributeWeights = new List<AttributeWeight>();
                string[] eachAnswer = WeightString.Split(',');
                for (int i = 0; i < eachAnswer.Length; i += 2)
                {
                    var attributeId = int.Parse(eachAnswer[i]);
                    var weight = int.Parse(eachAnswer[i + 1]);

                    attributeWeights.Add(new AttributeWeight(attributeId, weight));
                }
                return attributeWeights;
            }
            return null;
        }
    }
}
