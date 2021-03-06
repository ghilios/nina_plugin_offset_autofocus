using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NINA.Sequencer.Trigger.Autofocus {

    [Export(typeof(ResourceDictionary))]
    public partial class AutofocusWithOffsetAfterHFRIncreaseTriggerTemplate : ResourceDictionary {

        public AutofocusWithOffsetAfterHFRIncreaseTriggerTemplate() {
            InitializeComponent();
        }
    }
}