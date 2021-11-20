using Community.VisualStudio.Toolkit;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using ToreAurstadIT.Razor.Navigate.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace ToreAurstadIT.Razor.Navigate
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class Command1
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("382638f0-cdcb-469e-a3da-ac43527f870a");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="Command1"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private Command1(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static Command1 Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in Command1's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new Command1(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private async void Execute(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
            string title = "Command1";

            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
                return;
            var selection = docView.TextView.Selection;
            if (selection.IsEmpty)
            {
                return;
            }
            Solution currentSolution = await VS.Solutions.GetCurrentSolutionAsync();

            foreach (var snapshotSpan in selection.SelectedSpans)
            {
                string textOfSelection = snapshotSpan.GetText();
                if (string.IsNullOrWhiteSpace(textOfSelection))
                {
                    continue;
                }
                if (textOfSelection.Contains("@Html.Partial"))
                {
                    await ProcessHtmlPartialAsync(currentSolution, textOfSelection);
                }
                else if (textOfSelection.Contains(@"Html.RenderPartial"))
                {
                    await ProcessRenderPartialAsync(currentSolution, textOfSelection);
                }
            }


            //// Show a message box to prove we were here
            //VsShellUtilities.ShowMessageBox(
            //    this.package,
            //    message,
            //    title,
            //    OLEMSGICON.OLEMSGICON_INFO,
            //    OLEMSGBUTTON.OLEMSGBUTTON_OK,
            //    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        // Read the contents of the file.  
        static string GetFileText(string fileName)
        {
            string fileContents = String.Empty;

            // If the file has been deleted since we took
            // the snapshot, ignore it and return the empty string.  
            if (System.IO.File.Exists(fileName))
            {
                fileContents = System.IO.File.ReadAllText(fileName);
            }
            return fileContents;
        }

        static string ResolveConstant(string constantExpression, Solution solution)
        {
            try
            {

                if (constantExpression.Contains("."))
                {
                    constantExpression = constantExpression.Split('.').Last(); //we are after the member const - peel off class part
                }
                var fileWithConstantExpression =
                    (from file
                    in Directory.GetFiles(new FileInfo(solution.FullPath).Directory.FullName, "*.cs", SearchOption.AllDirectories)
                     where GetFileText(file).Contains(constantExpression)
                     select GetFileText(file)).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(fileWithConstantExpression))
                {
                    string pattern = $".*{constantExpression}.*(?<constantvalue>);";
                    Match m = Regex.Match(fileWithConstantExpression, pattern, RegexOptions.IgnoreCase);
                    if (m.Success && m.Groups.Count > 0)
                    {
                        //TODO: Rewrite - need an easier way to express this - multiple replaces
                        string constantValue = m.Groups[0].Value;
                        if (constantValue != null)
                        {
                            if (constantValue.Contains("="))
                            {
                                constantValue = constantValue.Split('=').Last();
                            }
                            if (constantValue.Contains("~"))
                            {
                                constantValue = constantValue.Replace("~", "");
                            }
                            if (constantValue.Contains(".."))
                            {
                                constantValue = constantValue.Replace("..", "");
                            }
                            if (constantValue.Contains("/"))
                            {
                                constantValue = constantValue.Split('/').Last();
                            }
                            if (constantValue.Contains("\""))
                            {
                                constantValue = constantValue.Replace("\"", string.Empty);
                            }
                            if (constantValue.Contains(";"))
                            {
                                constantValue = constantValue.Replace(";", "");
                            }

                            return constantValue.Trim();
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    
    private static async Task ProcessRenderPartialAsync(Solution currentSolution, string textOfSelection)
        {
            //TODO: we should also support the case where we do not pass in const expressions, i.e double quotes are present

            var pattern = @".*Html.RenderPartial\((?<razorfile>.*),.*";
            Match m = Regex.Match(textOfSelection, pattern, RegexOptions.IgnoreCase);
            if (m.Success)

                if (m.Groups["razorfile"]?.Value != null)
                {
                    string razorFileReference = m.Groups["razorfile"].Value;

                    razorFileReference = AdjustRazorFileReference(razorFileReference, currentSolution);                    

                    if (currentSolution != null)
                    {
                        string fullPathOfSolution = currentSolution.FullPath;                        

                        //scan for a file with matching file name (we already know it must be a cshtml or vbhtml file)
                        string[] foundFiles = Directory.GetFiles(new FileInfo(fullPathOfSolution).Directory.FullName, $"{razorFileReference}", SearchOption.AllDirectories);
                        if (foundFiles?.Length > 0)
                        {
                            string fileToOpen = foundFiles[0];
                            if (Path.GetExtension(fileToOpen)?.ToLower() != ".cshtml" && Path.GetExtension(fileToOpen)?.ToLower() != ".vbhtml")
                            {
                                return;
                            }
                            //on final check that the file exists on disk 

                            if (!File.Exists(fileToOpen))
                            {
                                return;
                            }

                            // also : check that we are permitted to open the file 

                            bool isFileAccessible = false;
                            try
                            {
                                _ = File.ReadAllText(fileToOpen);
                                isFileAccessible = true;
                            }
                            catch (Exception ex)
                            {
                                await VS.StatusBar.ShowMessageAsync($"Could not access the file {fileToOpen}. Reason: {ex.Message}");
                            }

                            if (!isFileAccessible)
                                return;

                            //just open the first matching razor file name for simplicity
                            bool isPartialViewAlreadyOpen = await VS.Documents.IsOpenAsync(foundFiles[0]);
                            if (!isPartialViewAlreadyOpen)
                            {
                                await VS.Documents.OpenAsync(foundFiles[0]);
                            }
                            else
                            {
                                await VS.Documents.OpenAsync(foundFiles[0]); //select document - set is as active as it is already opened ? 
                            }

                            await VS.StatusBar.ShowMessageAsync($"Navigated to razor file: {foundFiles[0]}");

                        }
                    }

                }

        }


        private async Task ProcessHtmlPartialAsync(Solution currentSolution, string textOfSelection)
        {

            var pattern = @".*@Html.Partial\((?<razorfile>.*)\).*";
            Match m = Regex.Match(textOfSelection, pattern, RegexOptions.IgnoreCase);
            if (m.Success)

                if (m.Groups["razorfile"]?.Value != null)
                {
                    string razorFileReference = m.Groups["razorfile"].Value;

                    razorFileReference = AdjustRazorFileReference(razorFileReference, currentSolution);

                    if (currentSolution != null)
                    {
                        string fullPathOfSolution = currentSolution.FullPath;
                        //scan for a file with matching file name (we already know it must be a cshtml or vbhtml file)
                        string[] foundFiles = Directory.GetFiles(new FileInfo(fullPathOfSolution).Directory.FullName, $"{razorFileReference}", SearchOption.AllDirectories);
                        if (foundFiles?.Length > 0)
                        {
                            string fileToOpen = foundFiles[0];
                            //double check that we got correct file extension 
                            if (Path.GetExtension(fileToOpen)?.ToLower() != ".cshtml" && Path.GetExtension(fileToOpen)?.ToLower() != ".vbhtml")
                            {
                                return;
                            }
                            //on final check that the file exists on disk 

                            if (!File.Exists(fileToOpen))
                            {
                                return;
                            }

                            // also : check that we are permitted to open the file 

                            bool isFileAccessible = false;
                            try
                            {
                                _ = File.ReadAllText(fileToOpen);
                                isFileAccessible = true;
                            }
                            catch (Exception ex)
                            {
                                await VS.StatusBar.ShowMessageAsync($"Could not access the file {fileToOpen}. Reason: {ex.Message}");
                            }

                            if (!isFileAccessible)
                                return;

                            if (foundFiles.Length == 1)
                            {

                                //just open the first matching razor file name for simplicity
                                bool isPartialViewAlreadyOpen = await VS.Documents.IsOpenAsync(foundFiles[0]);


                                if (!isPartialViewAlreadyOpen)
                                {

                                    await VS.Documents.OpenAsync(foundFiles[0]);
                                }
                                else
                                {
                                    await VS.Documents.OpenAsync(foundFiles[0]); //select document - set is as active as it is already opened ? 
                                }

                                await VS.StatusBar.ShowMessageAsync($"Navigated to razor file: {foundFiles[0]}");
                            }
                            else if (foundFiles.Length > 1)
                            {
                               string selectedCandidateFile =  await DisplayRazorFileViewChooserAsync(foundFiles);
                                if (!string.IsNullOrWhiteSpace(selectedCandidateFile))
                                {
                                    await VS.Documents.OpenAsync(selectedCandidateFile); //open the candidate file the end user selected

                                } //if 
                            }

                        }
                    }
                }

        }

        private async Task<string> DisplayRazorFileViewChooserAsync(string[] foundFiles)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken); //switch back to main thread before async stuff.

            IVsUIShell uiShell = await ServiceProvider.GetServiceAsync(typeof(SVsUIShell)) as IVsUIShell;
            ChoosePartialView choosePartialViewDlg = new ChoosePartialView();
            var vm = new ChoosePartialViewModel();
            vm.SetSelectableRazorViewFiles(foundFiles);
            choosePartialViewDlg.DataContext = vm;

            //get the owner of this dialog
            IntPtr hwnd;
            uiShell.GetDialogOwnerHwnd(out hwnd);
            choosePartialViewDlg.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            uiShell.EnableModeless(0);
            try
            {
                WindowHelper.ShowModal(choosePartialViewDlg, hwnd);
            }
            finally
            {
                // This will take place after the window is closed.
                uiShell.EnableModeless(1);
            }

            return vm.CandidateFile;
        }

        private static string AdjustRazorFileReference(string razorFileReference, Solution currentSolution)
        {
            if (razorFileReference.Contains("\"") && !razorFileReference.EndsWith(".cshtml", StringComparison.CurrentCultureIgnoreCase))
            {
                razorFileReference += ".cshtml"; //for now - only supporting cshtml files - need to inspect if the solution is using either CS or VB
            }

            if (razorFileReference.Contains("~"))
            {
                razorFileReference = razorFileReference.Replace("~", "");
            }
            if (razorFileReference.Contains("/"))
            {
                razorFileReference = razorFileReference.Split('/').Last(); //only after the file name
            }
            if (razorFileReference.Contains(".."))
            {
                razorFileReference = razorFileReference.Replace("..", "");
            }
            if (razorFileReference.Contains(','))
            {
                razorFileReference = razorFileReference.Split(',').First(); //usually we pass in Model as the next argument or other arguments such as controller name - we are after the name of the partial
            }

            //check if we got a constant expression too or not 

            //the constant must now be resolved (we passed in to RenderPartial not the " sign so we must have a constant
            if (!razorFileReference.Contains('"'))
            {
                string foundConstantValue = ResolveConstant(razorFileReference, currentSolution);
                if (!string.IsNullOrEmpty(foundConstantValue))
                {
                    razorFileReference = foundConstantValue;
                }
            }
            else
            {
                //trim away illeagal character '"' in path - we are soon going to scan for file names 

                razorFileReference = razorFileReference.Replace("\"", "");
            }

            //once more - since we are going to use Directory.GetFiles - we should suffix the file extension so we can actually find the file with the given razor file name 

            if (!razorFileReference.EndsWith(".cshtml", StringComparison.CurrentCultureIgnoreCase))
            {
                razorFileReference += ".cshtml";
            }

            return razorFileReference;
        }
    }
}
