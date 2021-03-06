using NINA.Model;
using NINA.Model.MyFilterWheel;
using NINA.Profile;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.Validations;
using NINA.Utility.Mediator.Interfaces;
using NINA.ViewModel.ImageHistory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Sequencer.Trigger.Autofocus {

    [ExportMetadata("Name", "AF Offset After Filter Change")]
    [ExportMetadata("Description", "Starts AutoFocus after a detected filter change, and adds a configurable offset to the result")]
    [ExportMetadata("Icon", "AutoFocusAfterFilterSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Focuser")]
    [Export(typeof(ISequenceTrigger))]
    public class AutofocusWithOffsetAfterFilterChange : SequenceTrigger, IValidatable {
        private IProfileService profileService;

        private IImageHistoryVM history;
        private ICameraMediator cameraMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IFocuserMediator focuserMediator;
        private IGuiderMediator guiderMediator;
        private IImagingMediator imagingMediator;
        private IApplicationStatusMediator applicationStatusMediator;

        [ImportingConstructor]
        public AutofocusWithOffsetAfterFilterChange(IProfileService profileService, IImageHistoryVM history, ICameraMediator cameraMediator, IFilterWheelMediator filterWheelMediator, IFocuserMediator focuserMediator, IGuiderMediator guiderMediator, IImagingMediator imagingMediator, IApplicationStatusMediator applicationStatusMediator) : base() {
            this.history = history;
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.focuserMediator = focuserMediator;
            this.guiderMediator = guiderMediator;
            this.imagingMediator = imagingMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            LastAutoFocusFilter = null;
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

        public FilterInfo LastAutoFocusFilter { get; private set; }

        public override object Clone() {
            return new AutofocusWithOffsetAfterFilterChange(profileService, history, cameraMediator, filterWheelMediator, focuserMediator, guiderMediator, imagingMediator, applicationStatusMediator) {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            await TriggerRunner.Run(progress, token);
        }

        public override void Initialize() {
            LastAutoFocusFilter = filterWheelMediator.GetInfo()?.SelectedFilter;
        }

        public override bool ShouldTrigger(ISequenceItem nextItem) {
            var currentFwInfo = filterWheelMediator.GetInfo();
            if (LastAutoFocusFilter == null) {
                LastAutoFocusFilter = currentFwInfo?.SelectedFilter;
                return false;
            } else {
                if (LastAutoFocusFilter != currentFwInfo?.SelectedFilter) {
                    LastAutoFocusFilter = currentFwInfo?.SelectedFilter;
                    return true;
                } else {
                    return false;
                }
            }
        }

        public override string ToString() {
            return $"Trigger: {nameof(AutofocusAfterFilterChange)}";
        }

        public bool Validate() {
            var i = new List<string>();
            var cameraInfo = cameraMediator.GetInfo();
            var focuserInfo = focuserMediator.GetInfo();
            var fwInfo = filterWheelMediator.GetInfo();

            if (!cameraInfo.Connected) {
                i.Add(Locale.Loc.Instance["LblCameraNotConnected"]);
            }
            if (!focuserInfo.Connected) {
                i.Add(Locale.Loc.Instance["LblFocuserNotConnected"]);
            }
            if (!fwInfo.Connected) {
                i.Add(Locale.Loc.Instance["LblFilterWheelNotConnected"]);
            }

            Issues = i;
            return i.Count == 0;
        }
    }
}