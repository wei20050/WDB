using System;
using System.Collections.Generic;
using System.Windows;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    public partial class SheZhi
    {
        /// <summary>
        /// 编辑中的完整模型列表(点"保存"才落盘)
        /// </summary>
        private List<AiModel> _models;

        public SheZhi()
        {
            InitializeComponent();
            RbIsClose.IsChecked = Config.IsClose;
            cbFontSize.Text = (Config.TextFontSize == 0 ? 16 : Config.TextFontSize).ToString();
            _models = Config.AiModels ?? new List<AiModel>();
            RefreshItems();
            //默认选中"最后一次使用的"配置,没有记录时用第一条
            dgAiModels.SelectedItem = _models.Find(p => p.Id == Config.AiModelId)
                                             ?? (_models.Count > 0 ? _models[0] : null);
        }

        #region AI 模型配置

        private void RefreshItems()
        {
            //ItemsSource 是 List(不是 ObservableCollection),增删后重设一次让列表刷新
            dgAiModels.ItemsSource = null;
            dgAiModels.ItemsSource = _models;
        }

        /// <summary>
        /// 从操作列按钮取到这一行对应的模型(按钮 Tag 绑定了行对象)
        /// </summary>
        private static AiModel RowModel(object sender)
        {
            return (sender as FrameworkElement)?.Tag as AiModel;
        }

        private void BtnAiAdd(object sender, RoutedEventArgs e)
        {
            //新增:空白表单弹窗(名称留空,由弹窗校验非空且不重复)
            var m = new AiModel { Id = Guid.NewGuid().ToString("N") };
            var dlg = new AiModelEdit(m, _models, "新建模型") { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _models.Add(m);
                RefreshItems();
                dgAiModels.SelectedItem = m;
            }
        }

        private void BtnRowEdit(object sender, RoutedEventArgs e)
        {
            var src = RowModel(sender);
            if (src == null)
            {
                return;
            }
            //修改:深拷贝当前配置进弹窗,保存成功后按 Id 替换原项
            var m = CoreConvert.Map<AiModel>(src);
            var dlg = new AiModelEdit(m, _models, "修改模型") { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                var idx = _models.FindIndex(p => p.Id == src.Id);
                if (idx >= 0)
                {
                    _models[idx] = m;
                }
                else
                {
                    _models.Add(m);
                }
                RefreshItems();
                dgAiModels.SelectedItem = m;
            }
        }

        private void BtnRowCopy(object sender, RoutedEventArgs e)
        {
            var src = RowModel(sender);
            if (src == null)
            {
                return;
            }
            var m = CoreConvert.Map<AiModel>(src);
            m.Id = Guid.NewGuid().ToString("N");
            m.Name = null;
            var dlg = new AiModelEdit(m, _models, "复制模型") { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                //复制项插在原项后面,方便对比着改
                var idx = _models.IndexOf(src);
                _models.Insert(idx + 1, m);
                RefreshItems();
                dgAiModels.SelectedItem = m;
            }
        }

        private void BtnRowDel(object sender, RoutedEventArgs e)
        {
            var src = RowModel(sender);
            if (src == null)
            {
                return;
            }
            var name = string.IsNullOrWhiteSpace(src.Name) ? "未命名" : src.Name;
            if (!Msg.Question($"确定删除配置「{name}」?"))
            {
                return;
            }
            var idx = _models.IndexOf(src);
            _models.Remove(src);
            RefreshItems();
            if (_models.Count > 0)
            {
                dgAiModels.SelectedItem = _models[Math.Min(idx, _models.Count - 1)];
            }
        }

        #endregion

        private void BtnBaocun(object sender, RoutedEventArgs e)
        {
            Config.IsClose = RbIsClose.IsChecked ?? false;

            //下拉框未选中或历史配置值不在列表中时 Text 可能为空,这里做容错避免保存时抛异常
            if (int.TryParse(cbFontSize.Text, out var fontSize) && fontSize > 0)
            {
                StaticVar.TextFontSize = fontSize;
                Config.TextFontSize = fontSize;
            }

            //名称是列表里的标识,不能为空,用模型名兜底,再不行用"未命名"
            foreach (var m in _models)
            {
                if (string.IsNullOrWhiteSpace(m.Name))
                {
                    m.Name = string.IsNullOrWhiteSpace(m.Model) ? "未命名" : m.Model.Trim();
                }
            }
            Config.AiModels = _models;
            //当前使用的模型被删掉时回退到第一个,避免提问时找不到配置
            if (_models.Find(p => p.Id == Config.AiModelId) == null)
            {
                Config.AiModelId = _models.Count > 0 ? _models[0].Id : string.Empty;
            }
            Close();
        }

        private void BtnQuxiao(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }
    }
}
