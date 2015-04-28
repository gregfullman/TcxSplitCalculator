using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;

namespace TcxSplitCalculator
{
    public enum TcxDistanceOption
    {
        Miles,
        Kilometers
    }

    public class SplitCalculator : INotifyPropertyChanged
    {
        public event EventHandler TcxModelChangedEvent;

        private readonly BackgroundWorker _calculatorThread;
        private TcxModel _tcxModel;
        public TcxModel TcxModel
        {
            get 
            { 
                return _tcxModel; 
            }
        }

        public SplitCalculator()
        {
            _calculatorThread = new BackgroundWorker();
            _calculatorThread.WorkerReportsProgress = true;
            _calculatorThread.WorkerSupportsCancellation = true;
            _calculatorThread.DoWork += _calculatorThread_DoWork;
            _calculatorThread.ProgressChanged += _calculatorThread_ProgressChanged;
            _calculatorThread.RunWorkerCompleted += _calculatorThread_RunWorkerCompleted;
        }

        void _calculatorThread_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetPropertyChanged("IsCalculating");
            CommandManager.InvalidateRequerySuggested();
            if(TcxModelChangedEvent != null)
            {
                TcxModelChangedEvent(this, new EventArgs());
            }
        }

        void _calculatorThread_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
        }

        void _calculatorThread_DoWork(object sender, DoWorkEventArgs e)
        {
            SetPropertyChanged("IsCalculating");
            _tcxModel = new TcxModel(Filename, DistanceOption, LapDistance);
        }

        public bool IsCalculating
        {
            get { return _calculatorThread.IsBusy; }
            set
            {
            }
        }

        private double _lapDistance = 1.00;
        public double LapDistance
        {
            get { return _lapDistance; }
            set
            {
                if(_lapDistance != value)
                {
                    _lapDistance = value;
                    SetPropertyChanged("LapDistance");
                }
            }
        }

        private string _filename;
        public string Filename
        {
            get { return _filename; }
            set
            {
                if(_filename != value)
                {
                    _filename = value;
                    SetPropertyChanged("Filename");
                }
            }
        }

        private TcxDistanceOption _distOption;
        public TcxDistanceOption DistanceOption
        {
            get { return _distOption; }
            set
            {
                if(_distOption != value)
                {
                    _distOption = value;
                    SetPropertyChanged("DistanceOption");
                }
            }
        }

        protected void SetPropertyChanged(string name)
        {
            if(PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private RelayCommand<object> _selectFileCommand;
        public ICommand SelectFileCommand
        {
            get
            {
                if(_selectFileCommand == null)
                {
                    _selectFileCommand = new RelayCommand<object>(param => SelectFile(), param => CanSelectFile());
                }
                return _selectFileCommand;
            }
        }

        private void SelectFile()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "TCX Files (.tcx)|*.tcx";
            ofd.FilterIndex = 0;
            ofd.Multiselect = false;
            bool? userClickedOk = ofd.ShowDialog();

            if(userClickedOk == true)
            {
                Filename = ofd.FileName;
            }
        }

        private bool CanSelectFile()
        {
            return !_calculatorThread.IsBusy;
        }

        private RelayCommand<object> _calculateCommand;
        public ICommand CalculateCommand
        {
            get
            {
                if(_calculateCommand == null)
                {
                    _calculateCommand = new RelayCommand<object>(param => StartCalculation(), param => CanStartCalculation());
                }
                return _calculateCommand;
            }
        }


        private void StartCalculation()
        {
            _calculatorThread.RunWorkerAsync();
        }

        private bool CanStartCalculation()
        {
            return !_calculatorThread.IsBusy;
        }
    }
}
