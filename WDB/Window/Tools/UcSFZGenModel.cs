using PropertyChanged;
using System.Collections.Generic;

namespace WDB.Window.Tools
{

    [AddINotifyPropertyChangedInterface]
    public class UcSFZGenModel
    {
        public List<string> SFZList { get; set; }
    }

}
