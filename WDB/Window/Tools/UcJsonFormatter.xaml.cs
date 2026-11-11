using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HandyControl.Data;
using Newtonsoft.Json.Linq;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class UcJsonFormatter :UserControl
    {
        public UcJsonFormatter()
        {
            InitializeComponent();
            DataContext = this;
            HighlightingProvider.Register(SkinType.Dark, new HighlightingProviderDark());
            TextEditor.TextArea.TextView.ElementGenerators.Add(new TruncateLongLines());
            TextEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "Json");
            TextEditor.TextArea.SelectionCornerRadius = 0;
            TextEditor.TextArea.SelectionBorder = null;
            TextEditor.TextArea.SelectionForeground = null;
            TextEditor.TextArea.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADD6FF"));
        }

        /// <summary>
        /// 返回
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = (ToolBox)System.Windows.Window.GetWindow(this);
            parentWindow.UcBox.Content = new MainTools();
        }

        /// <summary>
        /// 格式化Json
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnFormatter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextEditor.Text))
                {
                    Oops.Warning("请输入Json文本");
                    return;
                }
                TextEditor.Text = StrHelp.JsonFormatter(TextEditor.Text);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Oops.Warning("Json解析失败，请检查");
            }
        }

        /// <summary>
        /// 压缩Json
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCompress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextEditor.Text))
                {
                    Oops.Warning("请输入Json文本");
                    return;
                }
                TextEditor.Text = StrHelp.JsonCompress(TextEditor.Text);
            }
            catch (Exception)
            {
                Oops.Warning("Json解析失败，请检查");
            }
        }

        /// <summary>
        /// 复制文本
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextEditor.Text))
            {
                TextEditor.SelectAll();
                SafeClipboard.SetText(TextEditor.Text);
                Oops.Ok("文本已复制到剪切板");
            }
        }

        /// <summary>
        /// 清空输入框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.Text = string.Empty;
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            SafeClipboard.SetText(TextEditor.SelectedText);
        }

        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.Paste();
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.Delete();
        }

        private void Ningcunwei_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "JSON 文件(*.json)|*.json|所有文件(*.*)|*.*|文本文件(*.txt)|*.txt"; // 设置保存文件的筛选器
            saveFileDialog.FileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var fileName = saveFileDialog.FileName;
                TextEditor.Save(fileName);
            }
        }

        /// <summary>
        /// 格式化Json-C#
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnFormatterCshap_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextEditor.Text))
                {
                    Oops.Warning("请输入Json文本");
                    return;
                }
                TextEditor.Text = StrHelp.JsonFormatter(TextEditor.Text);


                JToken jToken = JToken.Parse(TextEditor.Text);
                StringBuilder sb = new StringBuilder();
                GenerateCSharpEntity(jToken, "Root", sb);

                SafeClipboard.SetText(sb.ToString());
                Oops.Ok("C#实体已复制到剪切板");
            }
            catch (Exception)
            {
                Oops.Warning("Json解析失败，请检查");
            }
        }

        /// <summary>
        /// 生成C#实体
        /// </summary>
        /// <param name="rootToken"></param>
        /// <param name="rootClassName"></param>
        /// <param name="sb"></param>
        private void GenerateCSharpEntity(JToken rootToken, string rootClassName, StringBuilder sb)
        {
            // 用来存储所有要生成的子类
            var subClasses = new StringBuilder();

            // 先生成主类，同时收集所有子类
            GenerateRootAndCollectSubClasses(rootToken, rootClassName, sb, subClasses);

            // 主类生成完后，追加所有子类
            sb.Append(subClasses);
        }

        /// <summary>
        /// 生成主类 + 收集所有子类
        /// </summary>
        private void GenerateRootAndCollectSubClasses(JToken jToken, string entityName, StringBuilder mainSb, StringBuilder subClassesSb)
        {
            if (jToken.Type != JTokenType.Object)
                return;

            // 1. 先生成主类
            mainSb.AppendLine("public class " + entityName);
            mainSb.AppendLine("{");

            foreach (JProperty property in jToken.Children<JProperty>())
            {
                string propName = property.Name;
                JToken value = property.Value;

                if (value.Type == JTokenType.Object)
                {
                    string childClass = entityName + "_" + propName;
                    // 递归生成子类到 subClassesSb
                    GenerateRootAndCollectSubClasses(value, childClass, subClassesSb, subClassesSb);
                    mainSb.AppendLine("    public " + childClass + " " + propName + " { get; set; }");
                }
                else if (value.Type == JTokenType.Array)
                {
                    JToken first = value.First;
                    if (first != null && first.Type == JTokenType.Object)
                    {
                        string childClass = entityName + "_" + propName + "Item";
                        GenerateRootAndCollectSubClasses(first, childClass, subClassesSb, subClassesSb);
                        mainSb.AppendLine("    public List<" + childClass + "> " + propName + " { get; set; }");
                    }
                    else
                    {
                        string type = first == null ? "object" : GetCSharpType(first.Type);
                        mainSb.AppendLine("    public List<" + type + "> " + propName + " { get; set; }");
                    }
                }
                else
                {
                    string csType = GetCSharpType(value.Type);
                    mainSb.AppendLine("    public " + csType + " " + propName + " { get; set; }");
                }
            }

            mainSb.AppendLine("}");
        }

        /// <summary>
        /// JSON类型映射C#类型
        /// </summary>
        private string GetCSharpType(JTokenType tokenType)
        {
            if (tokenType == JTokenType.Boolean)
                return "bool";
            if (tokenType == JTokenType.Integer)
                return "int";
            if (tokenType == JTokenType.Float)
                return "decimal";
            if (tokenType == JTokenType.String)
                return "string";
            if (tokenType == JTokenType.Date)
                return "DateTime";
            if (tokenType == JTokenType.Null)
                return "object";
            return "object";
        }

        /// <summary>
        /// 格式化Json-Java
        /// </summary>
        private void BtnFormatterJava_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextEditor.Text))
                {
                    Oops.Warning("请输入Json文本");
                    return;
                }
                TextEditor.Text = StrHelp.JsonFormatter(TextEditor.Text);

                JToken jToken = JToken.Parse(TextEditor.Text);
                StringBuilder sb = new StringBuilder();

                // Java 必备导包
                sb.AppendLine("import java.util.List;");
                sb.AppendLine("import java.util.Date;");
                sb.AppendLine();

                GenerateJavaEntity(jToken, "Root", sb);

                SafeClipboard.SetText(sb.ToString());
                Oops.Ok("Java实体已复制到剪切板");
            }
            catch (Exception)
            {
                Oops.Warning("Json解析失败，请检查");
            }
        }

        /// <summary>
        /// 生成Java实体（内部类，主类包含子类）
        /// </summary>
        private void GenerateJavaEntity(JToken jToken, string entityName, StringBuilder sb)
        {
            if (jToken.Type != JTokenType.Object)
                return;

            // 开始生成主类
            sb.AppendLine("public class " + entityName);
            sb.AppendLine("{");

            // 先生成所有属性
            foreach (JProperty property in jToken.Children<JProperty>())
            {
                string propName = property.Name;
                JToken value = property.Value;

                if (value.Type == JTokenType.Object)
                {
                    string childClass = entityName + "_" + propName;
                    sb.AppendLine("    private " + childClass + " " + propName + ";");
                    AppendJavaGetterSetter(sb, childClass, propName);
                }
                else if (value.Type == JTokenType.Array)
                {
                    JToken first = value.First;
                    if (first != null && first.Type == JTokenType.Object)
                    {
                        string childClass = entityName + "_" + propName + "Item";
                        sb.AppendLine("    private List<" + childClass + "> " + propName + ";");
                        AppendJavaGetterSetter(sb, "List<" + childClass + ">", propName);
                    }
                    else
                    {
                        string type = first == null ? "Object" : GetJavaType(first.Type);
                        sb.AppendLine("    private List<" + type + "> " + propName + ";");
                        AppendJavaGetterSetter(sb, "List<" + type + ">", propName);
                    }
                }
                else
                {
                    string javaType = GetJavaType(value.Type);
                    sb.AppendLine("    private " + javaType + " " + propName + ";");
                    AppendJavaGetterSetter(sb, javaType, propName);
                }
                sb.AppendLine();
            }

            // 生成所有内部子类
            foreach (JProperty property in jToken.Children<JProperty>())
            {
                string propName = property.Name;
                JToken value = property.Value;

                if (value.Type == JTokenType.Object)
                {
                    string childClass = entityName + "_" + propName;
                    GenerateJavaEntity(value, childClass, sb);
                }
                else if (value.Type == JTokenType.Array)
                {
                    JToken first = value.First;
                    if (first != null && first.Type == JTokenType.Object)
                    {
                        string childClass = entityName + "_" + propName + "Item";
                        GenerateJavaEntity(first, childClass, sb);
                    }
                }
            }

            // 结束主类
            sb.AppendLine("}");
        }

        /// <summary>
        /// 生成Java Getter/Setter
        /// </summary>
        private void AppendJavaGetterSetter(StringBuilder sb, string type, string fieldName)
        {
            string fieldUpper = char.ToUpper(fieldName[0]) + fieldName.Substring(1);

            sb.AppendLine("    public " + type + " get" + fieldUpper + "() {");
            sb.AppendLine("        return " + fieldName + ";");
            sb.AppendLine("    }");

            sb.AppendLine("    public void set" + fieldUpper + "(" + type + " " + fieldName + ") {");
            sb.AppendLine("        this." + fieldName + " = " + fieldName + ";");
            sb.AppendLine("    }");
        }

        /// <summary>
        /// JSON类型映射Java类型
        /// </summary>
        private string GetJavaType(JTokenType tokenType)
        {
            if (tokenType == JTokenType.Boolean)
                return "boolean";
            if (tokenType == JTokenType.Integer)
                return "int";
            if (tokenType == JTokenType.Float)
                return "double";
            if (tokenType == JTokenType.String)
                return "String";
            if (tokenType == JTokenType.Date)
                return "Date";
            if (tokenType == JTokenType.Null)
                return "Object";
            return "Object";
        }

    }
}
