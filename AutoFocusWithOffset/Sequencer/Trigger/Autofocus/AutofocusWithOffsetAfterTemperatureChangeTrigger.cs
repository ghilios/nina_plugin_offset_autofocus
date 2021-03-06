using Newtonsoft.Json;
using NINA.Model;
using NINA.Profile;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.Validations;
using NINA.Utility;
using NINA.Utility.Mediator.Interfaces;
using NINA.ViewModel.ImageHistory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Sequencer.Trigger.Autofocus {

    [ExportMetadata("Name", "AF Offset After Temperature Change")]
    [ExportMetadata("Description", "A trigger to initiate an autofocus run after the temperature has changed by the given amount in degrees since the last autofocus run or start of sequence, and adds a configurable offset to the result")]
    [ExportMetadata("Icon", "AutoFocusAfterTemperatureSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Focuser")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AutofocusWithOffsetAfterTemperatureChangeTrigger : SequenceTrigger, IValidatable {
        private IProfileService profileService;
        private IImageHistoryVM history;
        private ICameraMediator cameraMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IFocuserMediator focuserMediator;
        private IGuiderMediator guiderMediator;
        private IImagingMediator imagingMediator;
        private IApplicationStatusMediator applicationStatusMediator;
        private double initialTemperature;
        private bool initialized = false;

        [ImportingConstructor]
        public AutofocusWithOffsetAfterTemperatureChangeTrigger(IProfileService profileService, IImageHistoryVM history, ICameraMediator cameraMediator, IFilterWheelMediator filterWheelMediator, IFocuserMediator focuserMediator, IGuiderMediator guiderMediator, IImagingMediator imagingMediator, IApplicationStatusMediator applicationStatusMediator) : base() {
            this.history = history;
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.focuserMediator = focuserMediator;
            this.guiderMediator = guiderMediator;
            this.imagingMediator = imagingMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            Amount = 5;
            TriggerRunner.Add(new RunAutofocus(profileService, history, cameraMediator, filterWheelMediator, focuserMediator, guiderMediator, imagingMediator, applicationStatusMediator));
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = ImmutableList.CreateRange(value);
                RaisePropertyChanged();
            }
        }

        private int focuserDelta;

        [JsonProperty]
        public int FocuserDelta {
            get => focuserDelta;
            set {
                focuserDelta = value;
                RaisePropertyChanged();
            }
        }

        private double amount;

        [JsonProperty]
        public double Amount {
            get => amount;
            set {
                amount = value;
                RaisePropertyChanged();
            }
        }

        private double deltaT;

        public double DeltaT {
            get => deltaT;
            set {
                deltaT = value;
                RaisePropertyChanged();
            }
        }

        public override object Clone() {
            return new AutofocusWithOffsetAfterTemperatureChangeTrigger(profileService, history, cameraMediator, filterWheelMediator, focuserMediator, guiderMediator, imagingMediator, applicationStatusMediator) {
                Icon = Icon,
                Amount = Amount,
                Name = Name,
                Category = Category,
                Description = Description,
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            await TriggerRunner.Run(progress, token);

            if (focuserDelta != 0) {
                Logger.Info($"AutoFocus complete. Moving focuser an additional {focuserDelta} steps");
                await focuserMediator.MoveFocuserRelative(focuserDelta, token);
            }
        }

        public override void Initialize() {
            if (!initialized) {
                initialTemperature = focuserMediator.GetInfo()?.Temperature ?? double.NaN;
                initialized = true;
            }
        }

        public override bool ShouldTrigger(ISequenceItem nextItem) {
            var lastAF = history.AutoFocusPoints.LastOrDefault();
            var info = focuserMediator.GetInfo();
            if (lastAF == null && !double.IsNaN(initialTemperature)) {
                DeltaT = Math.Round(Math.Abs(initialTemperature - info.Temperature), 2);
                return Math.Abs(initialTemperature - info.Temperature) >= Amount;
            } else {
                DeltaT = Math.Round(Math.Abs(lastAF.AutoFocusPoint.Temperature - info.Temperature), 2);
                return Math.Abs(lastAF.AutoFocusPoint.Temperature - info.Temperature) >= Amount;
            }
        }

        public override string ToString() {
            return $"Trigger: {nameof(AutofocusAfterTemperatureChangeTrigger)}, Amount: {Amount}°";
        }

        public bool Validate() {
            var i = new List<string>();
            var cameraInfo = cameraMediator.GetInfo();
            var focuserInfo = focuserMediator.GetInfo();

            if (!cameraInfo.Connected) {
                i.Add(Locale.Loc.Instance["LblCameraNotConnected"]);
            }
            if (!focuserInfo.Connected) {
                i.Add(Locale.Loc.Instance["LblFocuserNotConnected"]);
            }

            Issues = i;
            return i.Count == 0;
        }
    }
}