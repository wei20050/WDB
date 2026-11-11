using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WDB
{
    // 可编辑的ListBox项模型
    public class EditableListItem : INotifyPropertyChanged
    {
        private string _content;
        private bool _isEditing;
        private string _originalContent; // 保存编辑前的原始值，用于取消编辑

        // 项的文本内容
        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                OnPropertyChanged();
            }
        }

        // 是否处于编辑状态
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                _isEditing = value;
                // 进入编辑状态时，保存原始值
                if (value) _originalContent = _content;
                OnPropertyChanged();
            }
        }

        // 取消编辑时恢复原始值
        public void RevertOriginalContent()
        {
            Content = _originalContent;
            IsEditing = false;
        }

        // 实现属性变更通知（WPF绑定必备）
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
