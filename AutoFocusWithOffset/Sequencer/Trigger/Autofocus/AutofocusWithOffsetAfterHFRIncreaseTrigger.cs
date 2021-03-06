using Accord.Math;
using Accord.Statistics.Models.Regression.Linear;
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

    [ExportMetadata("Name", "AF Offset After HFR Increase")]
    [ExportMetadata("Description", "A trigger to initiate an autofocus run after the HFR has increased by the given amount in percent since the last autofocus run or start of sequence, and adds a configurable offset to the result")]
    [ExportMetadata("Icon", "AutoFocusAfterHFRSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Focuser")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AutofocusWithOffsetAfterHFRIncreaseTrigger : SequenceTrigger, IValidatable {
        private IProfileService profileService;
        private IImageHistoryVM history;
        private ICameraMediator cameraMediator;
        private IFilterWheelMediator filterWheelMediator;
        private IFocuserMediator focuserMediator;
        private IGuiderMediator guiderMediator;
        private IImagingMediator imagingMediator;
        private IApplicationStatusMediator applicationStatusMediator;

        [ImportingConstructor]
        public AutofocusWithOffsetAfterHFRIncreaseTrigger(IProfileService profileService, IImageHistoryVM history, ICameraMediator cameraMediator, IFilterWheelMediator filterWheelMediator, IFocuserMediator focuserMediator, IGuiderMediator guiderMediator, IImagingMediator imagingMediator, IApplicationStatusMediator applicationStatusMediator) : base() {
            this.history = history;
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.focuserMediator = focuserMediator;
            this.guiderMediator = guiderMediator;
            this.imagingMediator = imagingMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            Amount = 5;
            SampleSize = 10;
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

        public override object Clone() {
            return new AutofocusWithOffsetAfterHFRIncreaseTrigger(profileService, history, cameraMediator, filterWheelMediator, focuserMediator, guiderMediator, imagingMediator, applicationStatusMediator) {
                Icon = Icon,
                Amount = Amount,
                Name = Name,
                Category = Category,
                Description = Description,
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };
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

        private int sampleSize;

        [JsonProperty]
        public int SampleSize {
            get => sampleSize;
            set {
                if (value >= 3) {
                    sampleSize = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double hFRTrend;

        public double HFRTrend {
            get => hFRTrend;
            private set {
                hFRTrend = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            await TriggerRunner.Run(progress, token);
        }

        public override bool ShouldTrigger(ISequenceItem nextItem) {
            var lastAF = history.AutoFocusPoints?.LastOrDefault();
            var minimumIndex = lastAF == null ? 0 : history.ImageHistory.FindIndex(x => x.Id == lastAF.Id) + 1;

            //Take either the Last AF Position as index or the sample size index, whichever is greater
            minimumIndex = Math.Max(minimumIndex, history.ImageHistory.Count - sampleSize);

            //at least 3 relevant points of data must exist
            if (history.ImageHistory.Count > minimumIndex + 3) {
                var data = history.ImageHistory
                    .Skip(minimumIndex)
                    .Where(x => !double.IsNaN(x.HFR) && x.HFR > 0);

                if (data.Count() > 3) {
                    double[] outputs = data
                        .Select(img => img.HFR)
                        .ToArray();

                    double[] inputs = data.Select(x => (double)x.Id).ToArray();

                    OrdinaryLeastSquares ols = new OrdinaryLeastSquares();
                    SimpleLinearRegression regression = ols.Learn(inputs, outputs);

                    //Get current smoothed out HFR
                    double currentHfrTrend = regression.Transform(history.ImageHistory.Count());
                    double originalHfr = regression.Transform(minimumIndex);

                    Logger.Debug($"Autofocus condition exrapolated original HFR: {originalHfr} extrapolated current HFR: {currentHfrTrend}");

                    HFRTrend = Math.Round((1 - (originalHfr / currentHfrTrend)) * 100, 2);

                    if (HFRTrend > Amount) {
                        /* Trigger autofocus after HFR change */
                        Logger.Debug($"Autofocus after HFR change has been triggered, as current HFR trend is {100 * (currentHfrTrend / originalHfr - 1)}% higher compared to threshold of {Amount}%");
                        return true;
                    }
                }
            } else {
                HFRTrend = 0;
            }
            return false;
        }

        public override void Initialize() {
        }

        public override string ToString() {
            return $"Trigger: {nameof(AutofocusAfterHFRIncreaseTrigger)}, Amount: {Amount}";
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