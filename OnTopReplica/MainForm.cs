﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using OnTopReplica.Properties;
using VistaControls.Dwm;
using VistaControls.TaskDialog;
using System.Collections.Generic;
using OnTopReplica.Native;

namespace OnTopReplica {

    partial class MainForm : AspectRatioForm {

        //GUI elements
        ThumbnailPanel _thumbnailPanel;
        SidePanel _currentSidePanel = null;
        Panel _sidePanelContainer;

        //Window manager
        WindowManager _windowManager = new WindowManager();

        //Message pump extension
        MessagePumpManager _msgPumpManager = new MessagePumpManager();

        FormBorderStyle _defaultBorderStyle;

        public MainForm() {
            InitializeComponent();
            KeepAspectRatio = false;

            //Store default values
            _defaultBorderStyle = FormBorderStyle;
            _nonClickThroughKey = TransparencyKey;

            //Thumbnail panel
            _thumbnailPanel = new ThumbnailPanel {
                Location = Point.Empty,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Size = ClientSize
            };
            _thumbnailPanel.CloneClick += new EventHandler<CloneClickEventArgs>(Thumbnail_CloneClick);
            Controls.Add(_thumbnailPanel);

            //Side panel
            _sidePanelContainer = new Panel {
                Location = new Point(ClientSize.Width, 0),
                Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                Enabled = false,
                Visible = false,
                Size = new Size(100, ClientSize.Height),
                Padding = new Padding(4)
            };
            Controls.Add(_sidePanelContainer);

            //Set native renderer on context menus
            Asztal.Szótár.NativeToolStripRenderer.SetToolStripRenderer(
                menuContext, menuWindows, menuOpacity, menuResize, menuLanguages, menuFullscreenContext
            );

            //Hook keyboard handler
            this.KeyUp += new KeyEventHandler(Form_KeyUp);
            this.KeyPreview = true;

            //Init message pump extensions
            _msgPumpManager.Initialize(this);

            //Add hotkeys
            var hotKeyMgr = _msgPumpManager.Get<MessagePumpProcessors.HotKeyManager>();
            hotKeyMgr.RegisterHotKey(Native.HotKeyModifiers.Control | Native.HotKeyModifiers.Shift,
                                     Keys.O, new Native.HotKeyMethods.HotKeyHandler(HotKeyOpenHandler));
            hotKeyMgr.RegisterHotKey(Native.HotKeyModifiers.Control | Native.HotKeyModifiers.Shift,
                                     Keys.C, new Native.HotKeyMethods.HotKeyHandler(HotKeyCloneHandler));
        }

        #region Event override

        protected override CreateParams CreateParams {
            get {
                //Needed to hide caption, while keeping window title in task bar
                var parms = base.CreateParams;
                parms.Style &= ~0x00C00000; //WS_CAPTION
                parms.Style &= 0x00040000; //WS_SIZEBOX
                return parms;
            }
        }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);

            //Platform specific form initialization
            Program.Platform.InitForm(this);

            //Glassify window
            GlassEnabled = true;
        }

        protected override void OnClosing(CancelEventArgs e) {
            _msgPumpManager.Dispose();

            base.OnClosing(e);
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);

            this.GlassMargins = (_currentSidePanel != null) ?
                new Margins(ClientSize.Width - _sidePanelContainer.Width, 0, 0, 0) :
                new Margins(-1);
        }

        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);

            //Deactivate click-through if reactivated
            if (ClickThroughEnabled) {
                ClickThroughEnabled = false;
            }

            Program.Platform.RestoreForm(this);
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);

            //HACK: sometimes, even if TopMost is true, the window loses its "always on top" status.
            //  This is an attempt of a fix that probably won't work...
            if (!IsFullscreen) { //fullscreen mode doesn't use TopMost
                TopMost = false;
                TopMost = true;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel(e);

            if (!IsFullscreen) {
                int change = (int)(e.Delta / 6.0); //assumes a mouse wheel "tick" is in the 80-120 range
                AdjustSize(change);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e) {
            base.OnMouseDoubleClick(e);

            //This is handled by the WM_NCLBUTTONDBLCLK msg handler usually (because the GlassForm translates
            //clicks on client to clicks on caption). But if fullscreen mode disables GlassForm dragging, we need
            //this auxiliary handler to switch mode.
            IsFullscreen = !IsFullscreen;
        }

        protected override void OnMouseClick(MouseEventArgs e) {
            base.OnMouseClick(e);

            //Same story as above...
            if (e.Button == System.Windows.Forms.MouseButtons.Right) {
                OpenContextMenu();
            }
        }

        protected override void WndProc(ref Message m) {
            if (_msgPumpManager.PumpMessage(ref m))
                return;

            switch (m.Msg) {
                case WM.NCRBUTTONUP:
                    //Open context menu if right button clicked on caption (i.e. all of the window area because of glass)
                    if (m.WParam.ToInt32() == HT.CAPTION) {
                        OpenContextMenu();

                        m.Result = IntPtr.Zero;
                        return;
                    }
                    break;

                case WM.NCLBUTTONDBLCLK:
                    //Toggle fullscreen mode if double click on caption (whole glass area)
                    if (m.WParam.ToInt32() == HT.CAPTION) {
                        IsFullscreen = !IsFullscreen;

                        m.Result = IntPtr.Zero;
                        return;
                    }
                    break;

                case WM.NCHITTEST:
                    //Make transparent to hit-testing if in click through mode
                    if (ClickThroughEnabled) {
                        m.Result = (IntPtr)HT.TRANSPARENT;
                        return;
                    }
                    break;
            }

            base.WndProc(ref m);
        }

        #endregion

        const string Title = "OnTopReplica";

        #region Keyboard event handling

        void Form_KeyUp(object sender, KeyEventArgs e) {
            //ALT
            if (e.Modifiers == Keys.Alt) {
                if (e.KeyCode == Keys.Enter) {
                    e.Handled = true;
                    IsFullscreen = !IsFullscreen;
                }

                else if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) {
                    FitToThumbnail(0.25);
                }

                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) {
                    FitToThumbnail(0.5);
                }

                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3 || e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) {
                    FitToThumbnail(1.0);
                }

                else if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) {
                    FitToThumbnail(2.0);
                }
            }

            //ESCAPE
            else if (e.KeyCode == Keys.Escape) {

#if DEBUG
                Console.WriteLine("Received ESCAPE");
#endif

                //Disable click-through
                if (ClickThroughEnabled) {
                    ClickThroughEnabled = false;
                }
                //Toggle fullscreen
                else if (IsFullscreen) {
                    IsFullscreen = false;
                }
                //Disable click forwarding
                else if (_thumbnailPanel.ReportThumbnailClicks) {
                    _thumbnailPanel.ReportThumbnailClicks = false;
                }
            }
        }

        void HotKeyOpenHandler() {
            if (IsFullscreen)
                IsFullscreen = false;

            if (Program.Platform.IsHidden(this)) {
                Program.Platform.HideForm(this);
            }
            else {
                EnsureMainFormVisible();
            }
        }

        void HotKeyCloneHandler() {
            var handle = Win32Helper.GetCurrentForegroundWindow();
            if (handle.Handle == this.Handle)
                return;

            SetThumbnail(handle, null);
        }

        #endregion

        #region Fullscreen

        bool _isFullscreen = false;
        Point _preFullscreenLocation;
        Size _preFullscreenSize;

        public bool IsFullscreen {
            get {
                return _isFullscreen;
            }
            set {
                if (IsFullscreen == value)
                    return;
                if (value && !_thumbnailPanel.IsShowingThumbnail)
                    return;

                CloseSidePanel(); //on switch, always hide side panels
                GlassEnabled = !value;
                FormBorderStyle = (value) ? FormBorderStyle.None : _defaultBorderStyle;
                TopMost = !value;
                HandleMouseMove = !value;

                //Location and size
                if (value) {
                    _preFullscreenLocation = Location;
                    _preFullscreenSize = Size;

                    var currentScreen = Screen.FromControl(this);
                    Size = currentScreen.WorkingArea.Size;
                    Location = currentScreen.WorkingArea.Location;
                }
                else {
                    Location = _preFullscreenLocation;
                    Size = _preFullscreenSize;
                    RefreshAspectRatio();
                }

                _isFullscreen = value;

                Program.Platform.OnFormStateChange(this);
            }
        }

        #endregion

        #region Thumbnail operation

        /// <summary>
        /// Sets a new thumbnail.
        /// </summary>
        /// <param name="handle">Handle to the window to clone.</param>
        /// <param name="region">Region of the window to clone.</param>
        public void SetThumbnail(WindowHandle handle, StoredRegion region) {
            try {
                CurrentThumbnailWindowHandle = handle;
                _thumbnailPanel.SetThumbnailHandle(handle);

                if (region != null)
                    _thumbnailPanel.SelectedRegion = region.Rect;
                else
                    _thumbnailPanel.ConstrainToRegion = false;

                //Set aspect ratio (this will resize the form), do not refresh if in fullscreen
                SetAspectRatio(_thumbnailPanel.ThumbnailOriginalSize, !IsFullscreen);
            }
            catch (Exception ex) {
                ThumbnailError(ex, false, Strings.ErrorUnableToCreateThumbnail);
            }
        }

        /// <summary>
        /// Enables group mode on a list of window handles.
        /// </summary>
        /// <param name="handles">List of window handles.</param>
        public void SetThumbnailGroup(IList<WindowHandle> handles) {
            if (handles.Count == 0)
                return;

            //At last one thumbnail
            SetThumbnail(handles[0], null);

            //Handle if no real group
            if (handles.Count == 1)
                return;

            CurrentThumbnailWindowHandle = null;
            _msgPumpManager.Get<MessagePumpProcessors.GroupSwitchManager>().EnableGroupMode(handles);
        }

        /// <summary>
        /// Disables the cloned thumbnail.
        /// </summary>
        public void UnsetThumbnail() {
            //Unset handle
            CurrentThumbnailWindowHandle = null;
            _thumbnailPanel.UnsetThumbnail();

            //Disable aspect ratio
            KeepAspectRatio = false;
        }

        /// <summary>
        /// Gets or sets the region displayed of the current thumbnail.
        /// </summary>
        public Rectangle? SelectedThumbnailRegion {
            get {
                if (!_thumbnailPanel.IsShowingThumbnail || !_thumbnailPanel.ConstrainToRegion)
                    return null;

                return _thumbnailPanel.SelectedRegion;
            }
            set {
                if (!_thumbnailPanel.IsShowingThumbnail)
                    return;

                if (value.HasValue) {
                    _thumbnailPanel.SelectedRegion = value.Value;
                    SetAspectRatio(value.Value.Size, true);
                }
                else {
                    _thumbnailPanel.ConstrainToRegion = false;
                    SetAspectRatio(_thumbnailPanel.ThumbnailOriginalSize, true);
                }
            }
        }

        private void ThumbnailError(Exception ex, bool suppress, string title) {
            if (!suppress) {
                ShowErrorDialog(title, Strings.ErrorGenericThumbnailHandleError, ex.Message);
            }

            UnsetThumbnail();
        }

        /// <summary>Automatically sizes the window in order to accomodate the thumbnail p times.</summary>
        /// <param name="p">Scale of the thumbnail to consider.</param>
        private void FitToThumbnail(double p) {
            try {
                Size originalSize = _thumbnailPanel.ThumbnailOriginalSize;
                Size fittedSize = new Size((int)(originalSize.Width * p), (int)(originalSize.Height * p));
                ClientSize = fittedSize;
            }
            catch (Exception ex) {
                ThumbnailError(ex, false, Strings.ErrorUnableToFit);
            }
        }

        #endregion

        #region Click-through

        bool _clickThrough = false;
        Color _nonClickThroughKey;

        public bool ClickThroughEnabled {
            get {
                return _clickThrough;
            }
            set {
                //Adjust opacity if fully opaque
                if (value && Opacity == 1.0)
                    Opacity = 0.75;
                if (!value)
                    Opacity = 1.0;

                //Enable transparency and force as top-most
                TransparencyKey = (value) ? Color.Black : _nonClickThroughKey;
                if (value)
                    TopMost = true;

                _clickThrough = value;
            }
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Gets the form's thumbnail panel.
        /// </summary>
        public ThumbnailPanel ThumbnailPanel {
            get {
                return _thumbnailPanel;
            }
        }

        /// <summary>
        /// Gets the form's message pump manager.
        /// </summary>
        public MessagePumpManager MessagePumpManager {
            get {
                return _msgPumpManager;
            }
        }

        /// <summary>
        /// Gets the form's window list drop down menu.
        /// </summary>
        public ContextMenuStrip MenuWindows {
            get {
                return menuWindows;
            }
        }

        /// <summary>
        /// Retrieves the window handle of the currently cloned thumbnail.
        /// </summary>
        public WindowHandle CurrentThumbnailWindowHandle {
            get;
            private set;
        }

        #endregion
        
    }
}
