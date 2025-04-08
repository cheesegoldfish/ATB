using System.Windows.Input;
using ATB.Commands;
using ATB.Models;
using ATB.Utilities;
using System.IO;

namespace ATB.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        public static MainSettingsModel Settings => MainSettingsModel.Instance;
        public static ICommand OverlayViewUpdate => new DelegateCommand(FormManager.OverlayToggle);

        private string _version;
        public string Version
        {
            get
            {
                if (string.IsNullOrEmpty(_version))
                {
                    var versionPath = Path.Combine(System.Environment.CurrentDirectory, @"BotBases\ATB\Version.txt");
                    if (File.Exists(versionPath))
                    {
                        _version = File.ReadAllText(versionPath).Trim();
                    }
                    else
                    {
                        _version = "Version Unknown";
                    }
                }
                return _version;
            }
        }
    }
}