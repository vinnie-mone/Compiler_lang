using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace GUI
{
    public partial class MainWindow : Window
    {
        private double _currentFontSize = 14;
        private Dictionary<TabItem, string> _tabPaths = new();
        private HashSet<TabItem> _modifiedTabs = new();

        public MainWindow()
        {
            InitializeComponent();

            this.Closing += MainWindow_Closing;
        }
        private (TabItem tab, TextBox lines, RichTextBox editor)CreateEditorTab(string fileName)
        {
            TabItem tab = new() { Header = fileName };

            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            TextBox lines = new()
            {
                IsReadOnly = true,
                Width = 45,
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = _currentFontSize,
                TextAlignment = TextAlignment.Right,
                Padding = new Thickness(0, 2, 5, 0),
                BorderThickness = new Thickness(0, 0, 1, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
            };

            RichTextBox editor = new()
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = _currentFontSize,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5, 2, 0, 0)
            };

            editor.TextChanged += DynamicEditor_TextChanged;
            editor.SelectionChanged += (s, e) => {
                string readyText = GetResourceString("StatusReady");
                UpdateCaretPosition(editor, $"{readyText}({fileName})");
            };
            editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(DynamicEditor_ScrollChanged));

            Grid.SetColumn(lines, 0);
            Grid.SetColumn(editor, 1);

            grid.Children.Add(lines);
            grid.Children.Add(editor);

            tab.Content = grid;

            EditorTabControl.Items.Add(tab);
            EditorTabControl.SelectedItem = tab;

            return (tab, lines, editor);
        }
        private (TextBox lines, RichTextBox editor) GetControlsFromTab(TabItem tab)
        {
            if (tab?.Content is not Grid grid)
                return (LineNumbersTextBox, EditorRichTextBox);

            return (
                grid.Children.OfType<TextBox>().FirstOrDefault(),
                grid.Children.OfType<RichTextBox>().FirstOrDefault()
            );
        }
        private void DynamicEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (e.Changes.Count == 0 || sender is not RichTextBox editor) return;

            var tab = EditorTabControl.SelectedItem as TabItem;
            var (lines, _) = GetControlsFromTab(tab);

            if (lines == null) return;

            UpdateLineNumbers(lines, editor);
            ApplyBasicHighlighting(editor);

            if (tab != null && _modifiedTabs.Add(tab))
            {
                if (!tab.Header.ToString().EndsWith("*"))
                    tab.Header += "*";
            }

            UpdateStatusAndCaret(tab);
        }

        private void DynamicEditor_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var tab = EditorTabControl.SelectedItem as TabItem;
            var (lines, _) = GetControlsFromTab(tab);
            lines?.ScrollToVerticalOffset(e.VerticalOffset);
        }
        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            DependencyObject parent = VisualTreeHelper.GetParent(btn);
            while (parent != null && parent is not TabItem)
                parent = VisualTreeHelper.GetParent(parent);

            if (parent is TabItem tab && PromptToSaveIfModified(tab))
            {
                _modifiedTabs.Remove(tab);
                _tabPaths.Remove(tab);
                EditorTabControl.Items.Remove(tab);
            }
        }
        private void CloseButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string outputText = GetResourceString("Input");

                string headerText = btn.DataContext?.ToString()?.TrimEnd('*');

                if (headerText == outputText)
                {
                    btn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    btn.Visibility = Visibility.Visible;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLineNumbers(LineNumbersTextBox, EditorRichTextBox);
            UpdateStatusAndCaret(EditorTabControl.SelectedItem as TabItem);
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
                OpenFileInNewTab(file);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            foreach (var tab in _modifiedTabs.ToList())
            {
                EditorTabControl.SelectedItem = tab;
                if (!PromptToSaveIfModified(tab))
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private TextPointer GetPointerAtOffset(TextPointer start, int offset)
        {
            TextPointer result = start;
            int i = 0;

            while (i < offset)
            {
                if (result.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    int textLen = result.GetTextInRun(LogicalDirection.Forward).Length;
                    if (i + textLen >= offset)
                    {
                        return result.GetPositionAtOffset(offset - i);
                    }
                    i += textLen;
                }
                else if (result.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementEnd &&
                         result.GetAdjacentElement(LogicalDirection.Forward) is Paragraph)
                {
                    i += 2;
                }

                TextPointer next = result.GetNextContextPosition(LogicalDirection.Forward);
                if (next == null) break;
                result = next;
            }
            return result;
        }
        private void NewFile_Click(object sender, RoutedEventArgs e) => CreateNewFile();
        private void OpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();
        private void SaveFile_Click(object sender, RoutedEventArgs e) => SaveFile();
        private void SaveAsFile_Click(object sender, RoutedEventArgs e) => SaveAsFile();
        private void IncreaseFont_Click(object sender, RoutedEventArgs e) => ChangeFontSize(2);
        private void DecreaseFont_Click(object sender, RoutedEventArgs e) => ChangeFontSize(-2);
        private void SetLangRu_Click(object sender, RoutedEventArgs e) => ChangeLanguage("Russian");
        private void SetLangEn_Click(object sender, RoutedEventArgs e) => ChangeLanguage("English");
        private void Exit_Click(object sender, RoutedEventArgs e) => Close();
        private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();
        private void About_Click(object sender, RoutedEventArgs e) => ShowAbout();
        private void NewFile_Click(object sender, ExecutedRoutedEventArgs e) => CreateNewFile();
        private void OpenFile_Click(object sender, ExecutedRoutedEventArgs e) => OpenFile();
        private void SaveFile_Click(object sender, ExecutedRoutedEventArgs e) => SaveFile();
    }
}