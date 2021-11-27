using Community.VisualStudio.Toolkit;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
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

                bool isUrlContentExpression = textOfSelection.Contains("Url.Content");
                bool isHtmlPartialExpression = textOfSelection.Contains("Html.Partial");
                bool isHtmlRenderPartialExpression = textOfSelection.Contains("Html.RenderPartial");
                bool isUrlActionExpression = textOfSelection.Contains("Url.Action"); 
              
                if (isHtmlPartialExpression)
                {
                    await ProcessHtmlPartialAsync(currentSolution, textOfSelection);
                }
                else if (isUrlContentExpression)
                {
                    await ProcessUrlContentAsync(currentSolution, textOfSelection);
                }
                else if (isHtmlRenderPartialExpression)
                {
                    await ProcessRenderPartialAsync(currentSolution, textOfSelection);
                } 
                else if (isUrlActionExpression)
                {
                    // TODO: support the scenario where we have parentheses around @Url.Action and also constants for controller name (resolve controller name constant value)
                    await ProcessUrlActionAsync(currentSolution, textOfSelection);
                }
            }

            #region generic_template_msgbox

            //// Show a message box to prove we were here
            //VsShellUtilities.ShowMessageBox(
            //    this.package,
            //    message,
            //    title,
            //    OLEMSGICON.OLEMSGICON_INFO,
            //    OLEMSGBUTTON.OLEMSGBUTTON_OK,
            //    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            #endregion 

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
    
    private async Task ProcessRenderPartialAsync(Solution currentSolution, string textOfSelection)
        {
            //TODO: we should also support the case where we do not pass in const expressions, i.e double quotes are present

            var pattern = @".*Html.RenderPartial\((?<razorfile>.*),.*";
            Match m = Regex.Match(textOfSelection, pattern, RegexOptions.IgnoreCase);
            if (m.Success)

                if (m.Groups["razorfile"]?.Value != null)
                {
                    string razorFileReference = m.Groups["razorfile"].Value;

                    (string resultPartOne, string resultPartTwo) = await AdjustRazorFileReference(razorFileReference, currentSolution, ".cshtml");                    
                    razorFileReference = resultPartOne;
                    string searchTermLookup = resultPartTwo;

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

        private async Task ProcessUrlContentAsync(Solution currentSolution, string textOfSelection)
        {
            var pattern = @".*Url.Content\((?<razorfile>.*)\).*";
            await GenericProcessMvcHtmlHelper(pattern, currentSolution, textOfSelection, ".js");
        }

        private async Task ProcessUrlActionAsync(Solution currentSolution, string textOfSelection)
        {
            var pattern = @".*Url.Action\((?<razorfile>.*).*";
            await GenericProcessMvcHtmlHelper(pattern, currentSolution, textOfSelection, ".cs");
        }


        private async Task ProcessHtmlPartialAsync(Solution currentSolution, string textOfSelection)
        {
            var pattern = @".*Html.Partial\((?<razorfile>.*)\).*";
            await GenericProcessMvcHtmlHelper(pattern, currentSolution, textOfSelection, ".cshtml");
        }

        private async Task GenericProcessMvcHtmlHelper(string pattern, Solution currentSolution, string textOfSelection, string expectedFileExtension)
        {
            Match m = Regex.Match(textOfSelection, pattern, RegexOptions.IgnoreCase);
            if (m.Success)

                if (m.Groups["razorfile"]?.Value != null)
                {
                    string razorFileReference = m.Groups["razorfile"].Value;

                    (string resultPartOne, string resultPartTwo) = await AdjustRazorFileReference(razorFileReference, currentSolution, expectedFileExtension);

                    razorFileReference = resultPartOne;
                    string searchTermToLookFor = resultPartTwo;

                    if (currentSolution != null)
                    {
                        string fullPathOfSolution = currentSolution.FullPath;
                        //scan for a file with matching file name (we already know it must be a cshtml or vbhtml file)
                        string[] foundFiles = Directory.GetFiles(new FileInfo(fullPathOfSolution).Directory.FullName, $"{razorFileReference}", SearchOption.AllDirectories);
                        if (foundFiles?.Length > 0)
                        {
                            string fileToOpen = foundFiles[0];

                            //CHANGE: allowing other file types now, since we support Url.Content

                            ////double check that we got correct file extension 
                            //if (Path.GetExtension(fileToOpen)?.ToLower() != ".cshtml" && Path.GetExtension(fileToOpen)?.ToLower() != ".vbhtml")
                            //{
                            //    return;
                            //}
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

                            bool isFileOpened = false;

                            if (foundFiles.Length == 1)
                            {

                                //just open the first matching razor file name for simplicity
                                bool isPartialViewAlreadyOpen = await VS.Documents.IsOpenAsync(foundFiles[0]);


                                if (!isPartialViewAlreadyOpen)
                                {

                                    await VS.Documents.OpenAsync(foundFiles[0]);
                                    isFileOpened = true;
                                }
                                else
                                {
                                    await VS.Documents.OpenAsync(foundFiles[0]); //select document - set is as active as it is already opened ? 
                                    isFileOpened = true;
                                }

                                await VS.StatusBar.ShowMessageAsync($"Navigated to razor file: {foundFiles[0]}");
                            }
                            else if (foundFiles.Length > 1)
                            {
                                string selectedCandidateFile = await DisplayRazorFileViewChooserAsync(foundFiles);
                                if (!string.IsNullOrWhiteSpace(selectedCandidateFile))
                                {
                                    await VS.Documents.OpenAsync(selectedCandidateFile); //open the candidate file the end user selected
                                    isFileOpened = true;

                                } //if 
                            }

                            if (isFileOpened)
                            {
                                await NavigateToSearchTermInFileAsync(searchTermToLookFor);
                            }

                        }
                    }
                }
        }

        private async Task NavigateToSearchTermInFileAsync(string searchTermToLookFor)
        {
            if (string.IsNullOrWhiteSpace(searchTermToLookFor))
            {
                return;
            }
            searchTermToLookFor = searchTermToLookFor.Trim().Replace("\"", string.Empty); //remove special chars

            var replaceRegex = new Regex("['[~()`]");

            searchTermToLookFor = replaceRegex.Replace(searchTermToLookFor, string.Empty); //do some more clean up of illeagal chars


            //check if we should also search for a term in the file 
            var currentDoc = await VS.Documents.GetActiveDocumentViewAsync();
            if (currentDoc != null && currentDoc.TextView.TextViewLines != null)
            {
                var snapshot = currentDoc.TextView.TextBuffer.CurrentSnapshot.GetText();

                if (snapshot != null)
                {
                    if (snapshot.Contains(searchTermToLookFor))
                    {
                        int textPosition = snapshot.IndexOf(searchTermToLookFor);
                        if (textPosition > 0)
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
                            currentDoc.TextView.Caret.MoveTo(new SnapshotPoint(currentDoc.TextView.TextBuffer.CurrentSnapshot, textPosition));
                            currentDoc.TextView.Caret.EnsureVisible();
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

        private static async Task<(string, string)> AdjustRazorFileReference(string razorFileReference,
            Solution currentSolution, string expectedFileExtension)
        {

            string searchTermInsideFile = null; 

            //remove dot-dot parent folder reference - this is done so we can analyze the file extension  

            if (razorFileReference.Contains(".."))
            {
                razorFileReference = razorFileReference.Replace("..", "");
            }

            if (razorFileReference.Contains("\"") && !razorFileReference.EndsWith(expectedFileExtension, StringComparison.CurrentCultureIgnoreCase))
            {
                if (expectedFileExtension == ".js")
                {
                    //special case analyse if we actually use Url.Content with an image instead - grab hold of extension 
                    if (razorFileReference.Contains("."))
                    {
                        string fileExtension = razorFileReference.Split('.')[1].Replace("\"", string.Empty);
                        if (fileExtension.Length >= 3)
                        {
                            expectedFileExtension = fileExtension.Substring(0, 3); //only handling the case with three letter extension file names for now
                        }
                    }
                }

                if (expectedFileExtension == ".cs")
                {
                    //in case we expect a ".cs" file it must be a Url.Action or Html.ActionLink - try to find out which controller it is 

                    if (razorFileReference.Contains(","))
                    {
                        //in case the file reference contains a comma, we must reference the controller name at second parameter - so look for anything
                        //named that + 'controller'

                        string[] razorFileReferenceArgs = razorFileReference.Split(',');
                        if (razorFileReferenceArgs.Count() > 1)
                        {
                            razorFileReference = razorFileReferenceArgs[1] + "Controller";

                            searchTermInsideFile = razorFileReferenceArgs[0]; //the action name should be looked up inside the file
                        }
                        else if (razorFileReferenceArgs.Count() == 0)
                        {
                            DocumentView currentDoc = await VS.Documents.GetActiveDocumentViewAsync();
                            if (currentDoc != null && !string.IsNullOrWhiteSpace(currentDoc.FilePath))
                            {
                                //MVC convention - if we only got the name of the action to navigate to,
                                //resolved parent folder which is equal to the controller name actually
                                var currentDir = new DirectoryInfo(currentDoc.FilePath).Parent.Name;
                                razorFileReference = currentDir + "Controller";

                                searchTermInsideFile = razorFileReferenceArgs[0]; 
                            }
                        }
                    }
                }

                if (!razorFileReference.Contains(expectedFileExtension)) { 
                    razorFileReference += expectedFileExtension; //for now - only supporting cshtml files - need to inspect if the solution is using either CS or VB
                }
            }

            var replaceRegex = new Regex("['[~()`]");

            razorFileReference = replaceRegex.Replace(razorFileReference, string.Empty); //do some clean up of illeagal chars

            if (!string.IsNullOrWhiteSpace(searchTermInsideFile))
            {
                searchTermInsideFile = replaceRegex.Replace(searchTermInsideFile, string.Empty).Trim();
            }

            if (razorFileReference.Contains("[~"))
            {
                razorFileReference = razorFileReference.Replace("~", "");
            }
            if (razorFileReference.Contains("/"))
            {
                razorFileReference = razorFileReference.Split('/').Last(); //only after the file name
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

            //lets also trim here - dont want trailing or preceeding whitespace. 

            razorFileReference = razorFileReference?.Trim(); 

            //once more - since we are going to use Directory.GetFiles - we should suffix the file extension so we can actually find the file with the given razor file name 

            //now supporting other file formats too, like .js files 

            //if (!razorFileReference.EndsWith(".cshtml", StringComparison.CurrentCultureIgnoreCase))
            //{
            //    razorFileReference += ".cshtml";
            //}

            return (razorFileReference, searchTermInsideFile);
        }
    }
}
