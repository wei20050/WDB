using PropertyChanged;
using System.Collections.Generic;

namespace WDB.Window.Tools
{
    [AddINotifyPropertyChangedInterface]
    public class UcUUIDGenModel
    {
        public List<UUIDResultDTO> UUIDList { get; set; }
    }

    [AddINotifyPropertyChangedInterface]
    public class UUIDResultDTO
    {
        public string UUID { get; set; }
    }
}
