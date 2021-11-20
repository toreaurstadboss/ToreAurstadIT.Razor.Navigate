using Microsoft.VisualStudio.PlatformUI;
using ToreAurstadIT.Razor.Navigate.ToolWindows;

namespace ToreAurstadIT.Razor.Navigate
{
    public partial class ChoosePartialView : DialogWindow
    {
        public ChoosePartialView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //Binding in WPF needs help from procedural code.  trying without ! 
            string selectedCandidateFile = this.lstBoxCandiateFiles.SelectedValue?.ToString();
            if (!string.IsNullOrEmpty(selectedCandidateFile))
            {
                //var vm = (ChoosePartialViewModel)this.DataContext;
                //vm.CandidateFile = vm.CandidateFile;
                this.Close();
            }
            else
            {
                MessageDialog.Show("ToreAurstadIT.Razor.Navigate", "Please select a file first. (Or close this dialog selecting the 'X' button.)", MessageDialogCommandSet.Ok);
            }

        }
    }
}
