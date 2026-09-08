using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using ATB.Models;
using ATB.Utilities;
using Buddy.Overlay;
using Buddy.Overlay.Controls;
using ff14bot;

namespace ATB.Views
{
    public partial class Overlay
    {
        public Overlay()
        {
            InitializeComponent();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            OverlayLogic.Stop();
        }

        private void UIElement_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                var currentDelta = e.Delta;
                if (currentDelta == 120)
                    MainSettingsModel.Instance.OverlayFontSize = MainSettingsModel.Instance.OverlayFontSize + 5;
                else
                {
                    MainSettingsModel.Instance.OverlayFontSize = MainSettingsModel.Instance.OverlayFontSize - 5;
                }
            }

            if (Control.ModifierKeys == Keys.Shift)
            {
                var currentDeltaOpacity = e.Delta;
                if (currentDeltaOpacity == 120 && MainSettingsModel.Instance.OverlayOpacity < 1)
                    MainSettingsModel.Instance.OverlayOpacity = MainSettingsModel.Instance.OverlayOpacity + 0.05;
                else
                {
                    MainSettingsModel.Instance.OverlayOpacity = MainSettingsModel.Instance.OverlayOpacity - 0.05;
                }

                if (MainSettingsModel.Instance.OverlayOpacity <= 0)
                {
                    MainSettingsModel.Instance.OverlayOpacity = .1;
                }

                if (MainSettingsModel.Instance.OverlayOpacity >= 1)
                {
                    MainSettingsModel.Instance.OverlayOpacity = 1;
                }
            }
        }
    }

    public static class OverlayLogic
    {
        public static bool ATBEnemyOverlayIsVisible;

        // Not readonly - must be recreatable for hot-reload to work correctly
        private static ATBEnemyOverlayUiComponent _overlayComponent;
        
        private static ATBEnemyOverlayUiComponent ATBOverlayComponent
        {
            get
            {
                return _overlayComponent ??= new ATBEnemyOverlayUiComponent(true);
            }
        }

        public static void Start()
        {
            if (!Core.OverlayManager.IsActive)
            {
                Core.OverlayManager.Activate();
            }
            ATBEnemyOverlayIsVisible = true;
            Core.OverlayManager.AddUIComponent(ATBOverlayComponent);
        }

        public static void Stop()
        {
            if (!Core.OverlayManager.IsActive)
                return;

            if (_overlayComponent != null)
            {
                Core.OverlayManager.RemoveUIComponent(_overlayComponent);
            }
            FormManager.SaveFormInstances();
            ATBEnemyOverlayIsVisible = false;
            
            // Reset component so it gets recreated with fresh XAML bindings on next Start()
            // This is critical for hot-reload - the old component references types from the unloaded assembly
            _overlayComponent = null;
        }
    }

    internal class ATBEnemyOverlayUiComponent : OverlayUIComponent
    {
        public ATBEnemyOverlayUiComponent(bool isHitTestable) : base(true)
        {
        }

        private OverlayControl _control;

        public override OverlayControl Control
        {
            get
            {
                if (_control != null)
                    return _control;

                // Create fresh Overlay UserControl - this ensures XAML bindings
                // reference types from the current assembly (important for hot-reload)
                var overlayUc = new Overlay();

                _control = new OverlayControl
                {
                    Name = "ATBEnemyOverlay",
                    Content = overlayUc,
                    X = MainSettingsModel.Instance.OverlayX,
                    Y = MainSettingsModel.Instance.OverlayY,
                    AllowMoving = true
                };

                _control.MouseLeave += (sender, args) =>
                {
                    MainSettingsModel.Instance.OverlayX = _control.X;
                    MainSettingsModel.Instance.OverlayY = _control.Y;
                    MainSettingsModel.Instance.Save();
                };

                return _control;
            }
        }

        /// <summary>
        /// Clears the cached control so it will be recreated on next access.
        /// Used during hot-reload to ensure fresh XAML bindings.
        /// </summary>
        internal void ResetControl()
        {
            _control = null;
        }
    }
}