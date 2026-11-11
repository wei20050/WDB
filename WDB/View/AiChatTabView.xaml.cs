using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using WDB.Common;
using WDB.Entity;

namespace WDB.View
{
    /// <summary>
    /// 独立页签里的 AI 对话页:仿网页版对话界面(消息气泡 + 底部输入框),
    /// 支持文字提问,也支持图片(选择文件/Ctrl+V 粘贴/拖拽),图片按 OpenAI 多模态格式随消息发送。
    /// 与右侧"AI助手"面板的区别:这里不绑定库/表上下文,是一条普通的通用对话。
    /// </summary>
    public partial class AiChatTabView : UserControl
    {
        /// <summary>发给模型的历史条数上限:太长既慢又容易被网关截断</summary>
        private const int MaxHistory = 20;

        /// <summary>一次最多带几张图片</summary>
        private const int MaxImages = 6;

        /// <summary>图片最大宽度:超过就等比缩小,避免请求体过大(识图不需要原图尺寸,多轮带图也不至于撑爆请求)</summary>
        private const int MaxImageWidth = 1024;

        /// <summary>图片最大高度:超长整页截图等比缩小,避免超大位图(RenderTarget/WIC 在大位图上有硬限制)和超标请求体</summary>
        private const int MaxImageHeight = 4096;

        private bool _busy;

        /// <summary>对话内容(界面绑它)</summary>
        private readonly ObservableCollection<AiChatBubble> _items = new ObservableCollection<AiChatBubble>();

        /// <summary>发给模型的历史(含本次提问;本次提问失败时撤掉)</summary>
        private readonly List<AIHelp.message> _history = new List<AIHelp.message>();

        /// <summary>待发送的图片(输入框上方预览,可单张移除)</summary>
        private readonly ObservableCollection<AiChatImage> _attach = new ObservableCollection<AiChatImage>();

        /// <summary>代码块:```lang ... ```(语言标识可有可无)</summary>
        private static readonly Regex CodeFenceRegex = new Regex(
            "```[a-zA-Z0-9#+\\-_. ]*\\r?\\n?([\\s\\S]*?)```", RegexOptions.Compiled);

        private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        public AiChatTabView()
        {
            InitializeComponent();
            IcChat.ItemsSource = _items;
            IcAttach.ItemsSource = _attach;
            RefreshModels();
            CbModel.SelectionChanged += CbModel_SelectionChanged;
        }

        #region 模型下拉

        /// <summary>
        /// 设置里改完模型配置后由外部调用:重读 Config.AiModels 刷新下拉,尽量保住当前选中项
        /// </summary>
        public void RefreshModels()
        {
            var models = Config.AiModels ?? new List<AiModel>();
            var keepId = (CbModel.SelectedItem as AiModel)?.Id ?? Config.AiModelId;
            CbModel.ItemsSource = models;
            CbModel.SelectedItem = models.Find(p => p.Id == keepId)
                                   ?? (models.Count > 0 ? models[0] : null);
        }

        /// <summary>
        /// 切换模型:记为"最后一次使用",右侧助手面板下次打开也用这个
        /// </summary>
        private void CbModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbModel.SelectedItem is AiModel model)
            {
                Config.AiModelId = model.Id;
            }
        }

        #endregion

        #region 提问

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            Send();
        }

        private void Send()
        {
            if (_busy)
            {
                return;
            }
            var text = (TxtInput.Text ?? string.Empty).Trim();
            //编码失败的图片发不出去,只发成功的
            var ready = _attach.Where(p => !string.IsNullOrEmpty(p.DataUrl)).ToList();
            var images = ready.Select(p => p.Thumbnail).ToList();
            var urls = ready.Select(p => p.DataUrl).ToList();
            if (_attach.Count > 0 && ready.Count < _attach.Count)
            {
                Msg.Warning("有图片编码失败,本次只发送文字和其它图片");
            }
            if (string.IsNullOrEmpty(text) && urls.Count == 0)
            {
                TxtInput.Focus();
                return;
            }
            var ai = CbModel.SelectedItem as AiModel ?? Config.AiModelCurrent();
            if (ai == null || string.IsNullOrWhiteSpace(ai.Url) || string.IsNullOrWhiteSpace(ai.Model))
            {
                AddBubble("请先在设置里配置 AI 模型(接口地址/模型名称/密钥)", false, true);
                return;
            }
            AddBubble(text, images, true, false);
            TxtInput.Clear();
            _attach.Clear();
            UpdateAttach();
            _history.Add(new AIHelp.message
            {
                role = "user",
                content = text,
                images = urls.Count > 0 ? urls : null
            });
            //先放一个空的 AI 气泡,收到多少显示多少(流式"打字机"效果)
            var reply = AddStreamingBubble();
            var seg = reply.Segments[0];
            SetBusy(true);
            _ = Task.Factory.StartNew(new Action(() =>
            {
                var sb = new StringBuilder();
                var lastFlush = DateTime.MinValue;
                try
                {
                    //普通对话提示词,温度适中;输出给宽一些,便于给完整代码
                    AIHelp.ChatMixedStream(ai.Url, ai.Model, ai.ApiKey, null, _history, delta =>
                    {
                        sb.Append(delta);
                        //每 ~50ms 刷一次界面:太少会卡,太多会抖
                        var now = DateTime.UtcNow;
                        if ((now - lastFlush).TotalMilliseconds < 50)
                        {
                            return;
                        }
                        lastFlush = now;
                        var snapshot = sb.ToString();
                        Dispatcher.BeginInvoke(new Action(() => UpdateStreaming(seg, snapshot)));
                    }, 0.7, 2000);

                    var full = sb.ToString();
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetBusy(false);
                        if (string.IsNullOrWhiteSpace(full))
                        {
                            //一个字都没回来:撤掉空气泡,按错误提示
                            _items.Remove(reply);
                            DropLastUserMessage();
                            AddBubble("AI 没有返回内容", false, true);
                            return;
                        }
                        FinishStreaming(reply, full);
                        _history.Add(new AIHelp.message { role = "assistant", content = full });
                        TrimHistory();
                    }));
                }
                catch (Exception ex)
                {
                    Log.Error("AI对话异常:" + ex);
                    var partial = sb.ToString();
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetBusy(false);
                        //这次提问没成:从历史里撤掉,下次追问不会带一个没有回答的问题
                        DropLastUserMessage();
                        if (string.IsNullOrWhiteSpace(partial))
                        {
                            _items.Remove(reply);
                            AddBubble("AI 访问失败,请检查设置里的接口地址/模型/密钥:" + ex.Message, false, true);
                        }
                        else
                        {
                            //已经吐了一部分:保留内容并注明中断
                            FinishStreaming(reply, partial + Environment.NewLine + Environment.NewLine + "(输出中断:" + ex.Message + ")");
                        }
                    }));
                }
            }));
        }

        /// <summary>撤掉最后一条用户历史(请求失败时用)</summary>
        private void DropLastUserMessage()
        {
            if (_history.Count > 0 && _history[_history.Count - 1].role == "user")
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        /// <summary>新增一个只有空段落的 AI 气泡,返回给流式更新用</summary>
        private AiChatBubble AddStreamingBubble()
        {
            var bubble = new AiChatBubble();
            bubble.Segments.Add(new AiChatSegment());
            _items.Add(bubble);
            TxtEmpty.Visibility = Visibility.Collapsed;
            ScrollToEnd();
            return bubble;
        }

        /// <summary>
        /// 流式过程中的一次刷新:直接改段落文本,不重建界面
        /// </summary>
        private void UpdateStreaming(AiChatSegment seg, string text)
        {
            seg.Text = text;
            TxtThinking.Visibility = Visibility.Collapsed;
            ScrollToEnd();
        }

        /// <summary>
        /// 流式结束:按 ``` 重新切段,让代码块用等宽+灰底显示
        /// </summary>
        private void FinishStreaming(AiChatBubble bubble, string text)
        {
            bubble.Text = text;
            bubble.Segments.Clear();
            AddSegments(bubble.Segments, text);
            ScrollToEnd();
        }

        private void TrimHistory()
        {
            while (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }
        }

        /// <summary>
        /// 请求期间禁用发送,并在对话末尾显示等待提示
        /// </summary>
        private void SetBusy(bool busy)
        {
            _busy = busy;
            BtnSend.IsEnabled = !busy;
            BtnImage.IsEnabled = !busy;
            TxtThinking.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            ScrollToEnd();
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            _items.Clear();
            _history.Clear();
            _attach.Clear();
            UpdateAttach();
            TxtInput.Clear();
            TxtEmpty.Visibility = Visibility.Visible;
            FocusInput();
        }

        /// <summary>打开页签后把焦点放进输入框</summary>
        public void FocusInput()
        {
            _ = Dispatcher.BeginInvoke(new Action(() => TxtInput.Focus()), DispatcherPriority.Loaded);
        }

        #endregion

        #region 图片

        private void BtnImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择图片",
                Filter = "图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true)
            {
                return;
            }
            foreach (var file in dlg.FileNames)
            {
                AddImageFile(file);
            }
        }

        /// <summary>点输入区里的预览缩略图:打开大图查看</summary>
        private void AttachImage_Click(object sender, MouseButtonEventArgs e)
        {
            ShowImage(((sender as FrameworkElement)?.Tag as AiChatImage)?.Thumbnail);
            e.Handled = true;
        }

        /// <summary>点对话气泡里的图片:打开大图查看</summary>
        private void BubbleImage_Click(object sender, MouseButtonEventArgs e)
        {
            ShowImage((sender as FrameworkElement)?.Tag as BitmapSource);
            e.Handled = true;
        }

        /// <summary>用独立窗口看大图(非模态,可以边看边继续对话)</summary>
        private void ShowImage(BitmapSource src)
        {
            if (src == null)
            {
                return;
            }
            var win = new WDB.Window.ImageView(src);
            var owner = System.Windows.Window.GetWindow(this);
            if (owner != null)
            {
                win.Owner = owner;
            }
            win.Show();
        }

        private void BtnRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is AiChatImage img)
            {
                _attach.Remove(img);
                UpdateAttach();
            }
        }

        /// <summary>
        /// 从文件加载一张图片(立即读完再交给界面,避免文件被占用;超宽会等比缩小)
        /// </summary>
        private void AddImageFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return;
                }
                //先整块读进内存再解码:不占文件句柄,也不受路径/网络盘等影响
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                {
                    var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (dec.Frames.Count == 0)
                    {
                        Msg.Warning("图片没有可显示的内容:" + Path.GetFileName(path));
                        return;
                    }
                    AddImageSource(dec.Frames[0]);
                }
            }
            catch (Exception ex)
            {
                Log.Error("加载图片失败:" + ex);
                Msg.Warning("图片加载失败:" + ex.Message);
            }
        }

        /// <summary>
        /// 粘贴剪贴板里的图片(截图后直接 Ctrl+V)
        /// </summary>
        private void AddClipboardImage()
        {
            try
            {
                //优先取剪贴板里的 PNG:比 DIB 更完整(带透明度信息),Win+Shift+S、浏览器复制等大多提供
                var data = Clipboard.GetDataObject();
                if (data != null && data.GetDataPresent("PNG"))
                {
                    var raw = data.GetData("PNG");
                    var ms = raw as MemoryStream ?? (raw is byte[] bytes ? new MemoryStream(bytes) : null);
                    if (ms != null)
                    {
                        ms.Position = 0;
                        var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        if (dec.Frames.Count > 0)
                        {
                            AddImageSource(dec.Frames[0]);
                            return;
                        }
                    }
                }
                //退回系统位图(截图工具常见:alpha 全 0,由 Normalize 兜底)
                var img = Clipboard.GetImage();
                if (img != null)
                {
                    AddImageSource(img);
                }
                else
                {
                    Msg.Warning("剪贴板里没有图片");
                }
            }
            catch (Exception ex)
            {
                Log.Error("粘贴图片失败:" + ex);
                Msg.Warning("粘贴图片失败:" + ex.Message);
            }
        }

        private void AddImageSource(BitmapSource src)
        {
            if (src == null)
            {
                return;
            }
            if (_attach.Count >= MaxImages)
            {
                Msg.Warning($"一次最多发送 {MaxImages} 张图片");
                return;
            }
            try
            {
                //一次算完:统一格式、处理透明度、按需缩小(不再走 WPF 的惰性缩放/合成链)
                var thumb = Normalize(src);
                if (thumb == null)
                {
                    Msg.Warning("这张图片没有可用的像素内容");
                    return;
                }
                _attach.Add(new AiChatImage
                {
                    Thumbnail = thumb,
                    DataUrl = TryToDataUrl(thumb)
                });
                UpdateAttach();
            }
            catch (Exception ex)
            {
                Log.Error("图片处理失败:" + ex);
                Msg.Warning("图片处理失败:" + ex.Message);
            }
        }

        /// <summary>
        /// 统一成界面上一定能正常显示的位图:自己逐行拷像素再算,不用 WPF 的
        /// TransformedBitmap / RenderTargetBitmap 那条惰性转换+合成链
        /// (大截图在那种链上会整片变黑,小图正常,所以表现为"大截图黑、小截图好")。
        /// 一遍扫描同时做三件事:
        /// 1) alpha 全 0 的剪贴板 DIB(截图工具/浏览器常见)按不透明处理,RGB 才是内容;
        /// 2) 真有透明区域的图铺白底,透明处不会变黑;
        /// 3) 超宽/超高按盒式平均等比缩小(请求体控制在几 MB 内)。
        /// </summary>
        private static BitmapSource Normalize(BitmapSource src)
        {
            var bgra = src.Format == PixelFormats.Bgra32
                ? src
                : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);

            var w = bgra.PixelWidth;
            var h = bgra.PixelHeight;
            if (w <= 0 || h <= 0)
            {
                return null;
            }

            //目标尺寸:只缩不放
            var scale = Math.Min(1.0, Math.Min((double)MaxImageWidth / w, (double)MaxImageHeight / h));
            var tw = Math.Max(1, (int)Math.Round(w * scale));
            var th = Math.Max(1, (int)Math.Round(h * scale));

            //抽样判断是否存在不透明像素:没有的话说明 alpha 是假的,直接用 RGB
            var blendAlpha = HasOpaquePixel(bgra);

            var stride = w * 4;
            var row = new byte[stride];
            var acc = new long[tw * 3];
            var cnt = new int[tw];
            var output = new byte[tw * th * 4];
            var currentTy = -1;
            for (var y = 0; y < h; y++)
            {
                var ty = (int)((long)y * th / h);
                if (ty != currentTy)
                {
                    if (currentTy >= 0)
                    {
                        WriteRow(output, acc, cnt, tw, currentTy);
                        Array.Clear(acc, 0, acc.Length);
                        Array.Clear(cnt, 0, cnt.Length);
                    }
                    currentTy = ty;
                }

                bgra.CopyPixels(new Int32Rect(0, y, w, 1), row, stride, 0);
                for (var x = 0; x < w; x++)
                {
                    var i = x * 4;
                    int b = row[i];
                    int g = row[i + 1];
                    int r = row[i + 2];
                    if (blendAlpha)
                    {
                        var a = row[i + 3];
                        if (a < 255)
                        {
                            b = (b * a + 255 * (255 - a)) / 255;
                            g = (g * a + 255 * (255 - a)) / 255;
                            r = (r * a + 255 * (255 - a)) / 255;
                        }
                    }
                    var tx = (int)((long)x * tw / w);
                    var o = tx * 3;
                    acc[o] += b;
                    acc[o + 1] += g;
                    acc[o + 2] += r;
                    cnt[tx]++;
                }
            }
            if (currentTy >= 0)
            {
                WriteRow(output, acc, cnt, tw, currentTy);
            }

            var result = BitmapSource.Create(tw, th, 96, 96, PixelFormats.Bgra32, null, output, tw * 4);
            result.Freeze();
            return result;
        }

        /// <summary>把一个目标行的累加值写成不透明像素(盒式平均)</summary>
        private static void WriteRow(byte[] output, long[] acc, int[] cnt, int tw, int ty)
        {
            var o = ty * tw * 4;
            for (var x = 0; x < tw; x++)
            {
                var n = cnt[x] > 0 ? cnt[x] : 1;
                output[o] = (byte)(acc[x * 3] / n);
                output[o + 1] = (byte)(acc[x * 3 + 1] / n);
                output[o + 2] = (byte)(acc[x * 3 + 2] / n);
                output[o + 3] = 255;
                o += 4;
            }
        }

        /// <summary>
        /// 抽样检查是否存在不透明像素(逐行取样,避免大图整块拷贝)
        /// </summary>
        private static bool HasOpaquePixel(BitmapSource src)
        {
            var w = src.PixelWidth;
            var h = src.PixelHeight;
            if (w <= 0 || h <= 0)
            {
                return false;
            }
            var stride = w * 4;
            var row = new byte[stride];
            var step = Math.Max(1, h / 64);
            for (var y = 0; y < h; y += step)
            {
                src.CopyPixels(new Int32Rect(0, y, w, 1), row, stride, 0);
                for (var x = 3; x < stride; x += 4)
                {
                    if (row[x] > 8)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 转成 data URL(OpenAI 多模态格式里图片用 base64 data URL 传)。
        /// 编码失败只影响"发图",不影响界面上的缩略图显示
        /// </summary>
        private static string TryToDataUrl(BitmapSource src)
        {
            try
            {
                return ToDataUrl(src);
            }
            catch (Exception ex)
            {
                Log.Error("图片编码失败:" + ex);
                return null;
            }
        }

        /// <summary>
        /// 直接转 Bgr24 编码:Normalize 之后一定是不透明像素,不用再铺白底;JPEG 编码器只吃 BGR 格式
        /// </summary>
        private static string ToDataUrl(BitmapSource src)
        {
            var bgr = src.Format == PixelFormats.Bgr24
                ? src
                : new FormatConvertedBitmap(src, PixelFormats.Bgr24, null, 0);
            bgr.Freeze();

            var enc = new JpegBitmapEncoder { QualityLevel = 88 };
            enc.Frames.Add(BitmapFrame.Create(bgr));
            using (var ms = new MemoryStream())
            {
                enc.Save(ms);
                return "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>刷新待发送图片区与提示文案</summary>
        private void UpdateAttach()
        {
            IcAttach.Visibility = _attach.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtAttach.Text = _attach.Count > 0
                ? $"已添加 {_attach.Count} 张图片,将随本次提问一起发送"
                : "支持 jpg / png,需选择支持识图的模型";
        }

        private void InputBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void InputBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }
            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] files))
            {
                return;
            }
            foreach (var file in files.Where(p => ImageExts.Contains(Path.GetExtension(p).ToLowerInvariant())))
            {
                AddImageFile(file);
            }
            e.Handled = true;
        }

        #endregion

        #region 对话内容

        private void AddBubble(string content, bool isUser, bool isError)
        {
            AddBubble(content, null, isUser, isError);
        }

        private void AddBubble(string content, List<BitmapSource> images, bool isUser, bool isError)
        {
            var bubble = new AiChatBubble { Text = content ?? string.Empty, IsUser = isUser, IsError = isError };
            AddSegments(bubble.Segments, content);
            if (images != null)
            {
                foreach (var img in images)
                {
                    bubble.Images.Add(img);
                }
            }
            bubble.HasImages = bubble.Images.Count > 0;
            _items.Add(bubble);
            TxtEmpty.Visibility = Visibility.Collapsed;
            ScrollToEnd();
        }

        /// <summary>
        /// 把回复按 ``` 代码块切成段落:代码用等宽字体+灰底显示,其余按普通文字换行显示
        /// </summary>
        private static void AddSegments(ObservableCollection<AiChatSegment> list, string text)
        {
            var src = text ?? string.Empty;
            var pos = 0;
            foreach (Match m in CodeFenceRegex.Matches(src))
            {
                if (m.Index > pos)
                {
                    var plain = src.Substring(pos, m.Index - pos).Trim('\r', '\n');
                    if (!string.IsNullOrWhiteSpace(plain))
                    {
                        list.Add(new AiChatSegment { Text = plain });
                    }
                }
                var code = m.Groups[1].Value.Trim('\r', '\n');
                if (!string.IsNullOrWhiteSpace(code))
                {
                    list.Add(new AiChatSegment { Text = code, IsCode = true });
                }
                pos = m.Index + m.Length;
            }
            if (pos < src.Length)
            {
                var tail = src.Substring(pos).Trim('\r', '\n');
                if (!string.IsNullOrWhiteSpace(tail))
                {
                    list.Add(new AiChatSegment { Text = tail });
                }
            }
            if (list.Count == 0 && !string.IsNullOrWhiteSpace(src))
            {
                list.Add(new AiChatSegment { Text = src.Trim() });
            }
        }

        /// <summary>
        /// 复制这条回复的全文(代码块也是原样的 ``` 文本,方便贴到编辑器或聊天工具里)
        /// </summary>
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is AiChatBubble bubble) || string.IsNullOrEmpty(bubble.Text))
            {
                return;
            }
            try
            {
                SafeClipboard.SetText(bubble.Text);
            }
            catch (Exception ex)
            {
                Log.Error("复制失败:" + ex);
                Msg.Warning("复制失败:" + ex.Message);
            }
        }

        /// <summary>
        /// 滚到底部:等布局跑完再滚,否则新加的消息还没排出来,滚不到底
        /// </summary>
        private void ScrollToEnd()
        {
            _ = Dispatcher.BeginInvoke(new Action(() => Scroller.ScrollToEnd()), DispatcherPriority.Background);
        }

        private void TxtInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //Ctrl+V:剪贴板里是图片就当作附件,是文字时走输入框默认粘贴
            if (e.Key == Key.V
                && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                && Clipboard.ContainsImage())
            {
                AddClipboardImage();
                e.Handled = true;
                return;
            }
            if (e.Key != Key.Enter)
            {
                return;
            }
            if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0)
            {
                //Shift/Ctrl+Enter 换行:在光标处插入并保持光标位置
                var box = (TextBox)sender;
                var caret = box.CaretIndex;
                box.Text = box.Text.Insert(caret, Environment.NewLine);
                box.CaretIndex = caret + Environment.NewLine.Length;
            }
            else
            {
                Send();
            }
            e.Handled = true;
        }

        #endregion
    }

    /// <summary>
    /// 对话里的一条消息:自己问的在右、AI 答的在左;出错的那条用浅红底
    /// </summary>
    public class AiChatBubble
    {
        /// <summary>完整正文(未拆段,用于复制)</summary>
        public string Text { get; set; }

        /// <summary>正文段落(代码块单独成段)</summary>
        public ObservableCollection<AiChatSegment> Segments { get; } = new ObservableCollection<AiChatSegment>();

        /// <summary>随消息一起发的图片(界面上按缩略图显示)</summary>
        public ObservableCollection<BitmapSource> Images { get; } = new ObservableCollection<BitmapSource>();

        public bool IsUser { get; set; }

        public bool IsError { get; set; }

        /// <summary>有没有图片(控制图片区显隐,添加时一次定好)</summary>
        public bool HasImages { get; set; }
    }

    /// <summary>
    /// 消息正文里的一段:普通文字或代码块。
    /// Text 支持变更通知:流式输出时只改文本,不重建界面
    /// </summary>
    public class AiChatSegment : INotifyPropertyChanged
    {
        private string _text;

        public string Text
        {
            get => _text;
            set
            {
                if (_text == value)
                {
                    return;
                }
                _text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }

        public bool IsCode { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// 待发送的图片:缩略图给界面显示,DataUrl 给请求用
    /// </summary>
    public class AiChatImage
    {
        public BitmapSource Thumbnail { get; set; }

        public string DataUrl { get; set; }
    }
}
