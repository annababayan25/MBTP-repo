namespace MBTP.Models
{
    public class NewbookStatus
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string RestAction { get; set; }
        public List<Params> RestParameters { get; set; }
        public string PeriodFrom { get; set; }
        public string PeriodTo { get; set; }
        public string ListType { get; set; }

    }

    public class Params
    {
        public string PeriodFrom { get; set; }
        public string PeriodTo { get; set; }
        public string ListType { get; set; }
    }
}