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

    [ExportMetadata("Name", "AF Offset After # Exposures")]
    [ExportMetadata("Description", "Triggers an autofocus run after the set amount of exposures have been captured, and adds a configurable offset to the result")]
    [ExportMetadata("Icon", "AutoFocusAfterExposuresSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Focuser")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AutofocusWithOffsetAfterExposures : SequenceTrigger, IValidatable {
        private IProfileService profileService;

        private IImageHistoryVM history;
        private ICameraMediator cameraMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IFocuserMediator focuserMediator;
        private IGuiderMediator guiderMediator;
        private IImagingMediator imagingMediator;
        private IApplicationStatusMediator applicationStatusMediator;

        [ImportingConstructor]
        public AutofocusWithOffsetAfterExposures(IProfileService profileService, IImageHistoryVM history, ICameraMediator cameraMediator, IFilterWheelMediator filterWheelMediator, IFocuserMediator focuserMediator, IGuiderMediator guiderMediator, IImagingMediator imagingMediator, IApplicationStatusMediator applicationStatusMediator) : base() {
            this.history = history;
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.focuserMediator = focuserMediator;
            this.guiderMediator = guiderMediator;
            this.imagingMediator = imagingMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            AfterExposures = 5;
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

        private int lastTriggerId = 0;

        private int focuserDelta;

        [JsonProperty]
        public int FocuserDelta {
            get => focuserDelta;
            set {
                focuserDelta = value;
                RaisePropertyChanged();
            }
        }

        private int afterExposures;

        [JsonProperty]
        public int AfterExposures {
            get => afterExposures;
            set {
                afterExposures = value;
                RaisePropertyChanged();
            }
        }

        public int ProgressExposures {
            get => history.ImageHistory.Count % AfterExposures;
        }

        public override object Clone() {
            return new AutofocusWithOffsetAfterExposures(profileService, history, cameraMediator, filterWheelMediator, focuserMediator, guiderMediator, imagingMediator, applicationStatusMediator) {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
                FocuserDelta = FocuserDelta,
                AfterExposures = AfterExposures,
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            lastTriggerId = history.ImageHistory.Count;
            await TriggerRunner.Run(progress, token);

            if (focuserDelta != 0) {
                Logger.Info($"AutoFocus complete. Moving focuser an additional {focuserDelta} steps");
                await focuserMediator.MoveFocuserRelative(focuserDelta, token);
            }
        }

        public override void Initialize() {
        }

        public override bool ShouldTrigger(ISequenceItem nextItem) {
            RaisePropertyChanged(nameof(ProgressExposures));
            var shouldTrigger =
                lastTriggerId < history.ImageHistory.Count
                && history.ImageHistory.Count > 0
                && ProgressExposures == 0
                && history.ImageHistory.Last().AutoFocusPoint == null;

            return shouldTrigger;
        }

        public override string ToString() {
            return $"Trigger: {nameof(AutofocusAfterExposures)}, AfterExposures: {AfterExposures}";
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