using Newtonsoft.Json;
using NINA.Model;
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Sequencer.Trigger.Autofocus {

    [ExportMetadata("Name", "AF Offset After Time")]
    [ExportMetadata("Description", "Triggers an autofocus run after the set amount of exposures have been captured, and adds a configurable offset to the result")]
    [ExportMetadata("Icon", "AutoFocusAfterTimeSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Focuser")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AutofocusWithOffsetAfterTimeTrigger : SequenceTrigger, IValidatable {
        private IProfileService profileService;
        private IImageHistoryVM history;
        private ICameraMediator cameraMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IFocuserMediator focuserMediator;
        private IGuiderMediator guiderMediator;
        private IImagingMediator imagingMediator;
        private IApplicationStatusMediator applicationStatusMediator;
        private DateTime initialTime;
        private bool initialized = false;

        [ImportingConstructor]
        public AutofocusWithOffsetAfterTimeTrigger(IProfileService profileService, IImageHistoryVM history, ICameraMediator cameraMediator, IFilterWheelMediator filterWheelMediator, IFocuserMediator focuserMediator, IGuiderMediator guiderMediator, IImagingMediator imagingMediator, IApplicationStatusMediator applicationStatusMediator) : base() {
            this.history = history;
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.focuserMediator = focuserMediator;
            this.guiderMediator = guiderMediator;
            this.imagingMediator = imagingMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            Amount = 30;
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

        private double amount;

        [JsonProperty]
        public double Amount {
            get => amount;
            set {
                amount = value;
                RaisePropertyChanged();
            }
        }

        private double elapsed;

        public double Elapsed {
            get => elapsed;
            private set {
                elapsed = value;
                RaisePropertyChanged();
            }
        }

        public override object Clone() {
            return new AutofocusWithOffsetAfterTimeTrigger(profileService, history, cameraMediator, filterWheelMediator, focuserMediator, guiderMediator, imagingMediator, applicationStatusMediator) {
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
        }

        public override void Initialize() {
            if (!initialized) {
                initialTime = DateTime.Now;
                initialized = true;
            }
        }

        public override bool ShouldTrigger(ISequenceItem nextItem) {
            var lastAF = history.AutoFocusPoints.LastOrDefault();
            if (lastAF == null) {
                Elapsed = Math.Round((DateTime.Now - initialTime).TotalMinutes, 2);
                return (DateTime.Now - initialTime) >= TimeSpan.FromMinutes(Amount);
            } else {
                Elapsed = Math.Round((DateTime.Now - lastAF.AutoFocusPoint.Time).TotalMinutes, 2);
                return (DateTime.Now - lastAF.AutoFocusPoint.Time) >= TimeSpan.FromMinutes(Amount);
            }
        }

        public override string ToString() {
            return $"Trigger: {nameof(AutofocusAfterTimeTrigger)}, Amount: {Amount}s";
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