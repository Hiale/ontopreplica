﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace OnTopReplica {

    /// <summary>
    /// Represents a side panel that can be embedded in OnTopReplica.
    /// </summary>
    class SidePanel : UserControl {

        public SidePanel() {
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Gets the panel's parent form.
        /// </summary>
        protected MainForm ParentForm { get; private set; }

        /// <summary>
        /// Raised when the side panel requests to be closed.
        /// </summary>
        public event EventHandler RequestClosing;

        protected virtual void OnRequestClosing() {
            var evt = RequestClosing;
            if (evt != null)
                evt(this, EventArgs.Empty);
        }

        /// <summary>
        /// Is called when the side panel is embedded and first shown.
        /// </summary>
        /// <param name="form">Parent form that is embedding the side panel.</param>
        public virtual void OnFirstShown(MainForm form) {
            ParentForm = form;
        }

        /// <summary>
        /// Is called before removing the side panel.
        /// </summary>
        /// <param name="form">Parent form that is embedding the side panel.</param>
        public virtual void OnClosing(MainForm form) {
        }

    }
}
