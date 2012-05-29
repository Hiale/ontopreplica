﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using OnTopReplica.Native;
using OnTopReplica.Properties;
using OnTopReplica.StartupOptions;
using OnTopReplica.Update;
using OnTopReplica.WindowSeekers;
using WindowsFormsAero.Dwm;
using WindowsFormsAero.TaskDialog;

namespace OnTopReplica {

    partial class MainForm : AspectRatioForm {

        //GUI elements
        ThumbnailPanel _thumbnailPanel;

        //Managers
        MessagePumpManager _msgPumpManager = new MessagePumpManager();
        WindowListMenuManager _windowListManager;

        Options _startupOptions;

        public MainForm(Options startupOptions) {
            _startupOptions = startupOptions;
            
            //WinForms init pass
            InitializeComponent();

            //Store default values
            DefaultNonClickTransparencyKey = this.TransparencyKey;
            DefaultBorderStyle = this.FormBorderStyle;

            //Thumbnail panel
            _thumbnailPanel = new ThumbnailPanel {
                Location = Point.Empty,
                Dock = DockStyle.Fill
            };
            _thumbnailPanel.CloneClick += new EventHandler<CloneClickEventArgs>(Thumbnail_CloneClick);
            Controls.Add(_thumbnailPanel);

            //Set native renderer on context menus
            Asztal.Szótár.NativeToolStripRenderer.SetToolStripRenderer(
                menuContext, menuWindows, menuOpacity, menuResize, menuFullscreenContext
            );

            //Set to Key event preview
            this.KeyPreview = true;
        }

        #region Event override

        protected override void OnHandleCreated(EventArgs e){
 	        base.OnHandleCreated(e);

            //Window init
            KeepAspectRatio = false;
            GlassEnabled = true;
            GlassMargins = new Margins(-1);

            //Managers
            _msgPumpManager.Initialize(this);
            _windowListManager = new WindowListMenuManager(this, menuWindows);
            _windowListManager.ParentMenus = new System.Windows.Forms.ContextMenuStrip[] {
                menuContext, menuFullscreenContext
            };

            //Platform specific form initialization
            Program.Platform.PostHandleFormInit(this);
        }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);

            //Apply startup options
            _startupOptions.Apply(this);
        }

        protected override void OnClosing(CancelEventArgs e) {
            //Store last thumbnail, if any
            if (_thumbnailPanel.IsShowingThumbnail && CurrentThumbnailWindowHandle != null) {
                Settings.Default.RestoreLastWindowTitle = CurrentThumbnailWindowHandle.Title;
                Settings.Default.RestoreLastWindowHwnd = CurrentThumbnailWindowHandle.Handle.ToInt64();
                Settings.Default.RestoreLastWindowClass = CurrentThumbnailWindowHandle.Class;
            }

            _msgPumpManager.Dispose();
            Program.Platform.CloseForm(this);

            base.OnClosing(e);
        }

        protected override void OnMove(EventArgs e) {
            base.OnMove(e);

            AdjustSidePanelLocation();
        }

        protected override void OnResizeEnd(EventArgs e) {
            base.OnResizeEnd(e);

            RefreshScreenLock();
        }

        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);

            //Deactivate click-through if form is reactivated
            if (ClickThroughEnabled) {
                ClickThroughEnabled = false;
            }

            Program.Platform.RestoreForm(this);
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);

            //HACK: sometimes, even if TopMost is true, the window loses its "always on top" status.
            //  This is a fix attempt that probably won't work...
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
                RefreshScreenLock();
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

            //Same story as above (OnMouseDoubleClick)
            if (e.Button == System.Windows.Forms.MouseButtons.Right) {
                OpenContextMenu();
            }
        }

        protected override void WndProc(ref Message m) {
            if (_msgPumpManager != null) {
                if (_msgPumpManager.PumpMessage(ref m)) {
                    return;
                }
            }

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
                    if (ClickThroughEnabled && (ModifierKeys & Keys.Alt) != Keys.Alt) {
                        m.Result = (IntPtr)HT.TRANSPARENT;
                        return;
                    }
                    break;
            }

            base.WndProc(ref m);
        }

        #endregion

        #region Keyboard event handling

        protected override void OnKeyUp(KeyEventArgs e) {
            base.OnKeyUp(e);

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

                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3 ||
                         e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) {
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
                else if (ClickForwardingEnabled) {
                    ClickForwardingEnabled = false;
                }
            }
        }

        #endregion

        #region Fullscreen

        bool _isFullscreen = false;
        Point _preFullscreenLocation;
        Size _preFullscreenSize;
        FormBorderStyle _preFullscreenBorderStyle;

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

                //Location and size
                if (value) {
                    _preFullscreenLocation = Location;
                    _preFullscreenSize = ClientSize;
                    _preFullscreenBorderStyle = FormBorderStyle;

                    FormBorderStyle = FormBorderStyle.None;
                    var currentScreen = Screen.FromControl(this);
                    Size = currentScreen.WorkingArea.Size;
                    Location = currentScreen.WorkingArea.Location;
                }
                else {
                    FormBorderStyle = _preFullscreenBorderStyle;
                    Location = _preFullscreenLocation;
                    ClientSize = _preFullscreenSize;
                    RefreshAspectRatio();
                }

                //Common
                GlassEnabled = !value;
                TopMost = !value;
                HandleMouseMove = !value;

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
        public void SetThumbnail(WindowHandle handle, Rectangle? region) {
            try {
                CurrentThumbnailWindowHandle = handle;
                _thumbnailPanel.SetThumbnailHandle(handle);

#if DEBUG
                string windowClass = WindowMethods.GetWindowClass(handle.Handle);
                Console.WriteLine("Cloning window HWND {0} of class {1}.", handle.Handle, windowClass);
#endif

                if (region.HasValue)
                    _thumbnailPanel.SelectedRegion = region.Value;
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

                FixPositionAndSize();
            }
        }

        const int FixMargin = 10;

        /// <summary>
        /// Fixes the form's position and size, ensuring it is fully displayed in the current screen.
        /// </summary>
        private void FixPositionAndSize() {
            var screen = Screen.FromControl(this);

            if (Width > screen.WorkingArea.Width) {
                Width = screen.WorkingArea.Width - FixMargin;
            }
            if (Height > screen.WorkingArea.Height) {
                Height = screen.WorkingArea.Height - FixMargin;
            }
            if (Location.X + Width > screen.WorkingArea.Right) {
                Location = new Point(screen.WorkingArea.Right - Width - FixMargin, Location.Y);
            }
            if (Location.Y + Height > screen.WorkingArea.Bottom) {
                Location = new Point(Location.X, screen.WorkingArea.Bottom - Height - FixMargin);
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
