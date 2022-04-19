namespace Sitecore.Feature.PublishedPageUrl.Models
{
    public class RootUrl
    {
        public string SiteName { get; set; }
        public string Language { get; set; }
        public string Url { get; set; }
        public int SortOrder { get; set; } = 0;
    }
}