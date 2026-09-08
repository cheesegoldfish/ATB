using ATB.Models;
using ATB.Models.Hotkeys;
using ATB.Views;
using ff14bot;

namespace ATB.Utilities
{
    internal class FormManager
    {
        private static ATBWindow _form;

        public static void SaveFormInstances()
        {
            MainSettingsModel.Instance.Save();
            ATBHotkeysModel.Instance.Save();
        }

        private static ATBWindow Form
        {
            get
            {
                if (_form != null) return _form;
                _form = new ATBWindow();
                _form.Closed += (sender, args) => _form = null;
                return _form;
            }
        }

        public static void OpenForms()
        {
            if (Form.IsVisible)
            {
                Form.Activate();
                return;
            }

            Form.Show();
        }

        /// <summary>
        /// Closes all ATB forms and overlays. Called during hot-reload so UI is recreated with fresh
        /// types from the new assembly; otherwise XAML bindings (especially enum ComboBoxes via
        /// ObjectDataProvider) keep referencing types from the unloaded assembly.
        /// </summary>
        public static void CloseAllForms()
        {
            if (_form != null)
            {
                try { _form.Close(); } catch { }
                _form = null;
            }

            if (OverlayLogic.ATBEnemyOverlayIsVisible)
                OverlayLogic.Stop();
        }

        internal static void OverlayToggle()
        {
            if (!TreeRoot.IsRunning) return;

            if (!MainSettingsModel.Instance.UseOverlay && OverlayLogic.ATBEnemyOverlayIsVisible)
                OverlayLogic.Stop();

            if (MainSettingsModel.Instance.UseOverlay && !OverlayLogic.ATBEnemyOverlayIsVisible)
                OverlayLogic.Start();
        }
    }
}