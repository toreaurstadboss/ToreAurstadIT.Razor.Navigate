using Microsoft.VisualStudio.PlatformUI;
using System.Windows;
using ToreAurstadIT.Razor.Navigate.ToolWindows;

namespace ToreAurstadIT.Razor.Navigate
{
    public partial class ChoosePartialView : DialogWindow
    {
        public ChoosePartialView()
        {
            InitializeComponent();
            this.Loaded += ChoosePartialView_Loaded;
        }

        private void ChoosePartialView_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.lstBoxCandidateFiles.Items.Count > 0)
            {
                this.lstBoxCandidateFiles.SelectedIndex = 0; //auto select for now the first candidate file for simplicity - TODO : fix up INotifyPropertyChanged 
            }
        }

        private void CloseButtonClick(object sender, System.Windows.RoutedEventArgs e)
        {
            string selectedCandidateFile = lstBoxCandidateFiles.SelectedValue?.ToString();
            var vm = this.DataContext as ChoosePartialViewModel;
            if (vm != null)
            {
                vm.CandidateFile = selectedCandidateFile;
            }
            if (string.IsNullOrEmpty(selectedCandidateFile))
            {
                MessageBox.Show("Select file first.");
                return;
            }
            this.Close();
        }
    }
}
