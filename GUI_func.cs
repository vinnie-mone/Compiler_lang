using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace GUI
{
    public partial class MainWindow
    {
        private void CreateNewFile()
        {
            var result = CreateEditorTab("New.txt");
            EditorTabControl.SelectedItem = result.tab;
            UpdateStatusAndCaret(result.tab);
        }

        private void OpenFile()
        {
            OpenFileDialog dlg = new();
            if (dlg.ShowDialog() == true)
            {
                OpenFileInNewTab(dlg.FileName);
            }
        }
        private string GetResourceString(string key) => Application.Current.TryFindResource(key)?.ToString() ?? key;
        private void OpenFileInNewTab(string path)
        {
            try
            {
                string content = File.ReadAllText(path);
                var result = CreateEditorTab(Path.GetFileName(path));

                result.editor.Document.Blocks.Clear();

                foreach (string line in content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                {
                    result.editor.Document.Blocks.Add(
                        new Paragraph(new Run(line))
                        { Margin = new Thickness(0) });
                }

                UpdateLineNumbers(result.lines, result.editor);
                EditorTabControl.SelectedItem = result.tab;

                _tabPaths[result.tab] = path;
                _modifiedTabs.Remove(result.tab);

                UpdateStatusAndCaret(result.tab);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                   $"{GetResourceString("MsgErrorOpen")}:\n{ex.Message}", 
                   "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveFile()
        {
            if (EditorTabControl.SelectedItem is not TabItem tab) return;

            if (_tabPaths.ContainsKey(tab))
                SaveContentToFile(_tabPaths[tab], tab);
            else
                SaveAsFile();
        }

        private void SaveAsFile()
        {
            if (EditorTabControl.SelectedItem is not TabItem tab) return;

            SaveFileDialog dlg = new();

            if (dlg.ShowDialog() != true) return;

            _tabPaths[tab] = dlg.FileName;
            tab.Header = Path.GetFileName(dlg.FileName);

            SaveContentToFile(dlg.FileName, tab);
        }

        private void SaveContentToFile(string path, TabItem tab)
        {
            var (lines, editor) = GetControlsFromTab(tab);
            if (editor == null) return;

            TextRange range = new(editor.Document.ContentStart, editor.Document.ContentEnd);

            try
            {
                File.WriteAllText(path, range.Text);
                _modifiedTabs.Remove(tab);
                tab.Header = Path.GetFileName(path);
                UpdateStatusAndCaret(tab);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{GetResourceString("MsgErrorSave")}:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool PromptToSaveIfModified(TabItem tab)
        {
            if (!_modifiedTabs.Contains(tab)) return true;
            string fileName = tab.Header.ToString().TrimEnd('*');
            
            var result = MessageBox.Show(
                $"{GetResourceString("MsgModified")}\n({fileName})",
                "Confirmation",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                EditorTabControl.SelectedItem = tab;
                SaveFile();
                return true;
            }

            return result == MessageBoxResult.No;
        }

        private void ChangeLanguage(string lang)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri($"Language/{lang}.xaml", UriKind.Relative)
            };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
            Refresh();
        }

        private void ChangeFontSize(double delta)
        {
            _currentFontSize =
                Math.Clamp(_currentFontSize + delta, 8, 72);

            foreach (TabItem tab in EditorTabControl.Items)
            {
                var (lines, editor) = GetControlsFromTab(tab);

                if (editor == null) continue;

                editor.FontSize = _currentFontSize;
                lines.FontSize = _currentFontSize;
                UpdateLineNumbers(lines, editor);
            }

            ResultTextBox.FontSize = _currentFontSize;
        }

        private void ApplyBasicHighlighting(RichTextBox editor)
        {
            editor.TextChanged -= DynamicEditor_TextChanged;

            try
            {
                TextPointer caretPos = editor.CaretPosition;

                foreach (var block in editor.Document.Blocks.OfType<Paragraph>())
                {
                    TextRange range = new(block.ContentStart, block.ContentEnd);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);
                    range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);

                    string[] keywords = { "if", "else", "while", "for", "return" };
                    foreach (string keyword in keywords)
                    {
                        Regex regex = new(@"\b" + keyword + @"\b");
                        foreach (Match match in regex.Matches(range.Text))
                        {
                            TextPointer start = GetPointerAtOffset(block.ContentStart, match.Index);
                            TextPointer end = GetPointerAtOffset(start, match.Length);

                            if (start != null && end != null)
                            {
                                TextRange wordRange = new(start, end);
                                wordRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Blue);
                                wordRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                            }
                        }
                    }
                }
                editor.CaretPosition = caretPos;
            }
            finally
            {
                editor.TextChanged += DynamicEditor_TextChanged;
            }
        }
        private void UpdateStatusAndCaret(TabItem tab)
        {
            if (tab == null) return;

            string name = tab.Header.ToString().TrimEnd('*');

            string readyText = GetResourceString("StatusReady");

            var (_, editor) = GetControlsFromTab(tab);

            UpdateCaretPosition(editor, $"{readyText}({name})");
        }
        private void UpdateCaretPosition(RichTextBox editor, string statusInfo)
        {
            if (editor == null) return;

            var caret = editor.CaretPosition;
            int line = 1;
            int column = 1;

            foreach (var paragraph in editor.Document.Blocks.OfType<Paragraph>())
            {
                if (paragraph.ContentStart.CompareTo(caret) <= 0 && caret.CompareTo(paragraph.ContentEnd) <= 0)
                {
                    column = new TextRange(paragraph.ContentStart, caret).Text.Length + 1;
                    break;
                }
                line++;
            }

            string caretInfo = string.Format(GetResourceString("CaretInfo"), line, column);

            StatusTextBlock.Text = $"{statusInfo}    {caretInfo}";
        }

        private void UpdateLineNumbers(TextBox lineNumbers, RichTextBox editor)
        {
            if (lineNumbers == null || editor == null) return;
            int lineCount = editor.Document.Blocks.Count;
            lineNumbers.Text = string.Join("\n", Enumerable.Range(1, Math.Max(1, lineCount)));
        }

        private string ReadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        private void ShowHelp()
        {
            string text = ReadEmbeddedResource("Compiler_lang.Resources.Help.txt");

            Window helpWindow = new()
            {
                Title = GetResourceString("MenuHelp"),
                Width = 600,
                Height = 400,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        Margin = new Thickness(10)
                    }
                }
            };

            helpWindow.ShowDialog();
        }

        private void ShowAbout()
        {
            string text = ReadEmbeddedResource("Compiler_lang.Resources.About.txt");

            Window aboutWindow = new()
            {
                Title = GetResourceString("MenuAbout"),
                Width = 400,
                Height = 300,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        Margin = new Thickness(10)
                    }
                }
            };

            aboutWindow.ShowDialog();
        }
        private void Refresh()
        {
            if (EditorTabControl.SelectedItem is TabItem selectedTab)
            {
                UpdateStatusAndCaret(selectedTab);
            }
        }
    }
}