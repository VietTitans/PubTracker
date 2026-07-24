using System.ComponentModel.DataAnnotations;

namespace RecordData;

class SearchQuery
{
    public int SourceId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public List<string>? Subscribers { get; set; }
    public TimestampAttribute? lastDigestSentAt { get; set; }

}
