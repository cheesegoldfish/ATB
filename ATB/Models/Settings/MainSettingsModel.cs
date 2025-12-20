using ATB.Commands;
using ff14bot;
using ff14bot.Managers;
using ff14bot.Objects;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Windows.Input;
using ATB.ViewModels;
using ATB.Utilities.Extensions;

namespace ATB.Models
{
    public class MainSettingsModel : BaseModel
    {
        private static LocalPlayer Me => Core.Player;
        private static MainSettingsModel _instance;
        public static MainSettingsModel Instance => _instance ?? (_instance = new MainSettingsModel());

        private MainSettingsModel() : base(@"Settings/" + Me.Name + "/ATB/Main_Settings.json")
        {
        }

        private bool _autoCommenceDuty, _autoDutyNotify, _usePull, _usePause, _useAutoFace, _useAutoTalk, _useAutoQuest, _useAutoCutscene, _useAutoTargeting,
            _useSmartPull, _useSmartFollow, _useExtremeCaution, _useAutoTpsAdjust, _outputToEcho, _useOverlay, _useToastMessages, _hideOverlayWhenRunning, _useStickyTargeting, _useStickyAuraTargeting,
            _autoSprint, _autoSprintInSanctuaryOnly, _pvpDetargetInvuln, _pvpSmartTargeting, _pvpDetargetGuard, _useQuickStartButton, _pvpPrioritizeMountedRobots, _pvpAutoTargetStopFlagCaptures,
            _autoLeaveDuty, _autoRegisterDuties;

        private int _autoCommenceDelay, _tpsAdjust, _overlayFontSize, _pvpSmartTargetingHp, _pvpStickiness, _secondsToAutoLeaveDuty, _secondsToAutoRegisterDuty, _dutyToRegister;

        private double _overlayWidth, _overlayHeight, _overlayX, _overlayY, _overlayOpacity;

        private float _maxTargetDistance;

        private WarmachinaTargetMode _pvpWarmachinaMode;

        [Setting]
        [DefaultValue(true)]
        public bool Pvp_DetargetGuard
        { get { return _pvpDetargetGuard; } set { _pvpDetargetGuard = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool Pvp_DetargetInvuln
        { get { return _pvpDetargetInvuln; } set { _pvpDetargetInvuln = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool Pvp_SmartTargeting
        { get { return _pvpSmartTargeting; } set { _pvpSmartTargeting = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(20)]
        public int Pvp_SmartTargetingHp
        { get { return _pvpSmartTargetingHp; } set { _pvpSmartTargetingHp = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(7)]
        public int Pvp_Stickiness
        { get { return _pvpStickiness; } set { _pvpStickiness = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(WarmachinaTargetMode.Ignore)]
        public WarmachinaTargetMode Pvp_WarmachinaMode
        { get { return _pvpWarmachinaMode; } set { _pvpWarmachinaMode = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool Pvp_PrioritizeMountedRobots
        { get { return _pvpPrioritizeMountedRobots; } set { _pvpPrioritizeMountedRobots = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool Pvp_AutoTargetStopFlagCaptures
        { get { return _pvpAutoTargetStopFlagCaptures; } set { _pvpAutoTargetStopFlagCaptures = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool AutoDutyNotify
        { get { return _autoDutyNotify; } set { _autoDutyNotify = value; OnPropertyChanged(); Save(); } }

        [Setting]
        [DefaultValue(true)]
        public bool AutoCommenceDuty
        { get { return _autoCommenceDuty; } set { _autoCommenceDuty = value; OnPropertyChanged(); Save(); } }

        [Setting]
        [DefaultValue(30)]
        public int AutoCommenceDelay
        { get { return _autoCommenceDelay; } set { _autoCommenceDelay = value; OnPropertyChanged(); Save(); } }

        [Setting]
        [DefaultValue(false)]
        public bool AutoLeaveDuty
        { get { return _autoLeaveDuty; } set { _autoLeaveDuty = value; OnPropertyChanged(); Save(); } }

        [Setting]
        [DefaultValue(30)]
        public int SecondsToAutoLeaveDuty
        { get { return _secondsToAutoLeaveDuty; } set { _secondsToAutoLeaveDuty = value; OnPropertyChanged(); Save(); } }

        [Setting]
        [DefaultValue(false)]
        public bool AutoRegisterDuties
        { get { return _autoRegisterDuties; } set { _autoRegisterDuties = value; OnPropertyChanged(); Save(); } }

        [Setting]
        [DefaultValue(30)]
        public int SecondsToAutoRegisterDuty
        { get { return _secondsToAutoRegisterDuty; } set { _secondsToAutoRegisterDuty = value; OnPropertyChanged(); Save(); } }

        [Setting]
        [DefaultValue(0)]
        public int DutyToRegister
        { get { return _dutyToRegister; } set { _dutyToRegister = value; OnPropertyChanged(); Save(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UsePull
        { get { return _usePull; } set { _usePull = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool UsePause
        { get { return _usePause; } set { _usePause = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseAutoFace
        { get { return _useAutoFace; } set { _useAutoFace = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(100)]
        public double OverlayWidth
        { get { return _overlayWidth; } set { _overlayWidth = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(50)]
        public double OverlayHeight
        { get { return _overlayHeight; } set { _overlayHeight = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(60)]
        public double OverlayX
        { get { return _overlayX; } set { _overlayX = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(60)]
        public double OverlayY
        { get { return _overlayY; } set { _overlayY = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(15)]
        public float MaxTargetDistance
        { get { return _maxTargetDistance; } set { _maxTargetDistance = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseAutoTalk
        { get { return _useAutoTalk; } set { _useAutoTalk = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseAutoQuest
        { get { return _useAutoQuest; } set { _useAutoQuest = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseAutoCutscene
        { get { return _useAutoCutscene; } set { _useAutoCutscene = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool UseAutoTargeting
        { get { return _useAutoTargeting; } set { _useAutoTargeting = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool UseStickyTargeting
        { get { return _useStickyTargeting; } set { _useStickyTargeting = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseStickyAuraTargeting
        { get { return _useStickyAuraTargeting; } set { _useStickyAuraTargeting = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseSmartPull
        { get { return _useSmartPull; } set { _useSmartPull = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseSmartFollow
        { get { return _useSmartFollow; } set { _useSmartFollow = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseExtremeCaution
        { get { return _useExtremeCaution; } set { _useExtremeCaution = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseAutoTpsAdjust
        { get { return _useAutoTpsAdjust; } set { _useAutoTpsAdjust = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(30)]
        public int TpsAdjust
        { get { return _tpsAdjust; } set { _tpsAdjust = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool UseOutputToEcho
        { get { return _outputToEcho; } set { _outputToEcho = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool UseQuickStartButton
        { get { return _useQuickStartButton; } set { _useQuickStartButton = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseOverlay
        { get { return _useOverlay; } set { _useOverlay = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(1)]
        public double OverlayOpacity
        { get { return _overlayOpacity; } set { _overlayOpacity = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(75)]
        public int OverlayFontSize
        { get { return _overlayFontSize; } set { _overlayFontSize = value; OverlayViewModel.Instance.OverlaySize = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(true)]
        public bool UseToastMessages
        { get { return _useToastMessages; } set { _useToastMessages = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool HideOverlayWhenRunning
        { get { return _hideOverlayWhenRunning; } set { _hideOverlayWhenRunning = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool AutoSprint
        { get { return _autoSprint; } set { _autoSprint = value; OnPropertyChanged(); } }

        [Setting]
        [DefaultValue(false)]
        public bool AutoSprintInSanctuaryOnly
        { get { return _autoSprintInSanctuaryOnly; } set { _autoSprintInSanctuaryOnly = value; OnPropertyChanged(); } }

        [JsonIgnore]
        private List<string> _combatRoutineList;

        [JsonIgnore]
        public List<string> CombatRoutineList => _combatRoutineList ?? (_combatRoutineList = GetCombatRoutines().ToList());

        private static IEnumerable<string> GetCombatRoutines()
        {
            var retval = new HashSet<string>();
            foreach (var routine in RoutineManager.AllRoutines.Select(x => x.Name).Where(x => x != "InvalidRoutineWrapper" && x != " "))
            {
                var index = routine.IndexOf('[');
                retval.Add(index > 0 ? routine.Substring(0, index) : routine);
            }

            retval.Add(string.Empty);

            return retval;
        }

        private volatile AutoTargetSelection _autoTargetSelection;

        [Setting]
        [DefaultValue(AutoTargetSelection.None)]
        public AutoTargetSelection AutoTargetSelection
        { get { return _autoTargetSelection; } set { _autoTargetSelection = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public ICommand ChangeAutoTargetSelectionCommand => new DelegateCommand(ChangeAutoTargetSelection);

        private void ChangeAutoTargetSelection()
        {
            switch (AutoTargetSelection)
            {
                case AutoTargetSelection.None:
                    AutoTargetSelection = AutoTargetSelection.NearestEnemy;
                    return;

                case AutoTargetSelection.NearestEnemy:
                    AutoTargetSelection = AutoTargetSelection.BestClustered;
                    return;

                case AutoTargetSelection.BestClustered:
                    AutoTargetSelection = AutoTargetSelection.LowestCurrentHp;
                    return;

                case AutoTargetSelection.LowestCurrentHpTanked:
                    AutoTargetSelection = AutoTargetSelection.None;
                    return;

                case AutoTargetSelection.LowestCurrentHp:
                    AutoTargetSelection = AutoTargetSelection.None;
                    return;

                case AutoTargetSelection.LowestTotalHpTanked:
                    AutoTargetSelection = AutoTargetSelection.None;
                    return;

                case AutoTargetSelection.LowestTotalHp:
                    AutoTargetSelection = AutoTargetSelection.None;
                    return;

                case AutoTargetSelection.HighestCurrentHpTanked:
                    AutoTargetSelection = AutoTargetSelection.None;
                    return;

                case AutoTargetSelection.HighestCurrentHp:
                    AutoTargetSelection = AutoTargetSelection.None;
                    return;

                case AutoTargetSelection.HighestTotalHpTanked:
                    AutoTargetSelection = AutoTargetSelection.None;
                    return;

                case AutoTargetSelection.HighestTotalHp:
                    AutoTargetSelection = AutoTargetSelection.None;
                    return;

                case AutoTargetSelection.TankAssist:
                    AutoTargetSelection = AutoTargetSelection.None;
                    break;
            }
        }

        private volatile AutoFollowSelection _autoFollowSelection;

        [Setting]
        [DefaultValue(AutoFollowSelection.None)]
        public AutoFollowSelection AutoFollowSelection
        { get { return _autoFollowSelection; } set { _autoFollowSelection = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public ICommand ChangeAutoFollowSelectionCommand => new DelegateCommand(ChangeAutoFollowSelection);

        private void ChangeAutoFollowSelection()
        {
            switch (AutoFollowSelection)
            {
                case AutoFollowSelection.None:
                    AutoFollowSelection = AutoFollowSelection.Smart;
                    return;

                case AutoFollowSelection.Smart:
                    AutoFollowSelection = AutoFollowSelection.None;
                    return;
            }
        }

        private SelectedTheme _selectedTheme;

        [Setting]
        [DefaultValue(SelectedTheme.Pink)]
        public SelectedTheme Theme
        { get { return _selectedTheme; } set { _selectedTheme = value; OnPropertyChanged(); } }
    }

    public enum AutoTargetSelection
    {
        None,
        NearestEnemy,
        BestClustered,
        LowestCurrentHpTanked,
        LowestCurrentHp,
        LowestTotalHpTanked,
        LowestTotalHp,
        HighestCurrentHpTanked,
        HighestCurrentHp,
        HighestTotalHpTanked,
        HighestTotalHp,
        TankAssist
    }

    public enum AutoFollowSelection
    {
        None,
        Smart
    }

    public enum SelectedTheme
    {
        Blue,
        Pink,
        Green,
        Red,
        Yellow
    }

    public enum WarmachinaTargetMode
    {
        Ignore,
        PrioritizeWarmachina,
        PrioritizePlayers
    }
}
