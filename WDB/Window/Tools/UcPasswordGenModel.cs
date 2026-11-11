using PropertyChanged;
using System.Collections.Generic;

namespace WDB.Window.Tools
{

    [AddINotifyPropertyChangedInterface]
    public class UcPasswordGenModel
    {
        public List<PasswordResultDTO> PasswordList { get; set; }
    }

    [AddINotifyPropertyChangedInterface]
    public class PasswordResultDTO
    {
        public string Password { get; set; }

        public int PasswordStrength { get; set; }
    }
}
