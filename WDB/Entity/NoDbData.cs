namespace WDB.Entity
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class NoDbData
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public long Size { get; set; }
        public long Ttl { get; set; }
        public bool IsEdit { get; set; }
    }
}
