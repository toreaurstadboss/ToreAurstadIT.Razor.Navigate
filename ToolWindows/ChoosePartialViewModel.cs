using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ToreAurstadIT.Razor.Navigate.ToolWindows
{
    public class ChoosePartialViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<string> CandidateFiles { get; set; }

        private string _candidateFile;
        public string CandidateFile
        {
            get { return _candidateFile; }
            set {
                if (_candidateFile != value)
                {
                    _candidateFile = value;
                    RaisePropertyChanged("CandidateFile");
                }
            }
        }

        public ChoosePartialViewModel()
        {
            CandidateFiles = new ObservableCollection<string>(); 
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (!string.IsNullOrWhiteSpace(propertyName))
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        public void SetSelectableRazorViewFiles(string[] candidateFiles)
        {
            CandidateFiles.Clear();
            if (candidateFiles != null)
            {
                foreach (var candidateFile in candidateFiles)
                {
                    CandidateFiles.Add(candidateFile);
                }
            }
            if (candidateFiles.Length > 0)
            {
                CandidateFile = candidateFiles[0]; //auto-select the first candidate file
            }
        }



    }
}
