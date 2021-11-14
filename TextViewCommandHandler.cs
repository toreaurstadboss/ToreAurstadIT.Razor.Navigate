using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;

namespace ShowSelectionLength
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public class CommandHandler : WpfTextViewCreationListener
    {
        protected override void Created(DocumentView docView)
        {
            docView.TextView.Selection.SelectionChanged += TextSelectionChanged;

           
        }

        protected override void Closed(IWpfTextView textView)
        {
            textView.Selection.SelectionChanged -= TextSelectionChanged;
        }

        private void TextSelectionChanged(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {

                var selection = (ITextSelection)sender;
                if (selection.IsEmpty)
                    return;
                foreach (var snapshotSpan in selection.SelectedSpans)
                {
                    string textOfSelection = snapshotSpan.GetText();
                    if (string.IsNullOrWhiteSpace(textOfSelection))
                    {
                        continue;
                    }
                    if (textOfSelection.Contains("@Html.Partial"))
                    {
                        var pattern = @".*@Html.Partial\(""(?<razorfile>.*)""\).*";
                        Match m = Regex.Match(textOfSelection, pattern, RegexOptions.IgnoreCase);
                        if (m.Success)

                            if (m.Groups["razorfile"]?.Value != null)
                            {
                                await VS.StatusBar.ShowMessageAsync($"You selected this razor file: {textOfSelection}");
                                
                            }
                    }
                }
                


                //var selection = (ITextSelection)sender;

                //if (selection.IsEmpty)
                //{
                //    await VS.StatusBar.ClearAsync();

                //    return;
                //}

                //var length = 0;

                //foreach (SnapshotSpan snapshotSpan in selection.SelectedSpans)
                //{
                //    length += snapshotSpan.Length;
                //}

                //if (length > 0)
                //{
                //    await VS.StatusBar.ShowMessageAsync($"Selection {length}");
                //}

            }).FireAndForget();
        }
    }
}

