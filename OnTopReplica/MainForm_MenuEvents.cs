﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using OnTopReplica.Properties;
using VistaControls.TaskDialog;

namespace OnTopReplica {
    partial class MainForm {

        private void Menu_opening(object sender, CancelEventArgs e) {
            //Cancel if currently in "fullscreen" mode or a side panel is open
            if (IsFullscreen || _currentSidePanel != null) {
                e.Cancel = true;
                return;
            }

            selectRegionToolStripMenuItem.Enabled = _thumbnailPanel.IsShowingThumbnail;
            switchToWindowToolStripMenuItem.Enabled = _thumbnailPanel.IsShowingThumbnail;
            resizeToolStripMenuItem.Enabled = _thumbnailPanel.IsShowingThumbnail;
            chromeToolStripMenuItem.Checked = (FormBorderStyle == FormBorderStyle.Sizable);
            forwardClicksToolStripMenuItem.Checked = _thumbnailPanel.ReportThumbnailClicks;
        }

        private void Menu_Windows_opening(object sender, CancelEventArgs e) {
            _windowManager.Refresh(WindowManager.EnumerationMode.TaskWindows);
            WindowListHelper.PopulateMenu(this, _windowManager, (ToolStrip)sender,
                _lastWindowHandle, new EventHandler(Menu_Windows_itemclick));
        }

        void Menu_Windows_itemclick(object sender, EventArgs e) {
            //Ensure the menu is closed
            menuContext.Close();
            menuFullscreenContext.Close();

            //Get clicked item and window index from tag
            ToolStripItem tsi = (ToolStripItem)sender;

            //Handle special "none" window
            if (tsi.Tag == null) {
                UnsetThumbnail();
                return;
            }

            var selectionData = (WindowListHelper.WindowSelectionData)tsi.Tag;
            SetThumbnail(selectionData.Handle, selectionData.Region);
        }

        private void Menu_Switch_click(object sender, EventArgs e) {
            if (_lastWindowHandle == null)
                return;

            Program.Platform.HideForm(this);
            Native.WindowManagerMethods.SetForegroundWindow(_lastWindowHandle.Handle);
        }

        private void Menu_GroupSwitchMode_click(object sender, EventArgs e) {
            SetSidePanel(new SidePanels.GroupSwitchPanel());
        }

        private void Menu_ClickForwarding_click(object sender, EventArgs e) {
            if (Settings.Default.FirstTimeClickForwarding && !_thumbnailPanel.ReportThumbnailClicks) {
                TaskDialog dlg = new TaskDialog(Strings.InfoClickForwarding, Strings.InfoClickForwardingTitle, Strings.InfoClickForwardingContent) {
                    CommonButtons = TaskDialogButton.Yes | TaskDialogButton.No
                };
                if (dlg.Show(this).CommonButton == Result.No)
                    return;

                Settings.Default.FirstTimeClickForwarding = false;
            }

            _thumbnailPanel.ReportThumbnailClicks = !_thumbnailPanel.ReportThumbnailClicks;
        }

        private void Menu_ClickThrough_click(object sender, EventArgs e) {
            ClickThroughEnabled = true;
        }

        private void Menu_Opacity_opening(object sender, CancelEventArgs e) {
            ToolStripMenuItem[] items = {
				toolStripMenuItem1,
				toolStripMenuItem2,
				toolStripMenuItem3,
				toolStripMenuItem4
			};

            foreach (ToolStripMenuItem i in items) {
                if ((double)i.Tag == this.Opacity)
                    i.Checked = true;
                else
                    i.Checked = false;
            }
        }

        private void Menu_Opacity_click(object sender, EventArgs e) {
            //Get clicked menu item
            ToolStripMenuItem tsi = sender as ToolStripMenuItem;

            if (tsi != null) {
                //Get opacity from the tag
                this.Opacity = (double)tsi.Tag;
            }
        }

        private void Menu_Region_click(object sender, EventArgs e) {
            SetSidePanel(new OnTopReplica.SidePanels.RegionPanel());
        }

        private void Menu_Resize_opening(object sender, CancelEventArgs e) {
            if (!_thumbnailPanel.IsShowingThumbnail)
                e.Cancel = true;
        }

        private void Menu_Resize_Double(object sender, EventArgs e) {
            FitToThumbnail(2.0);
        }

        private void Menu_Resize_FitToWindow(object sender, EventArgs e) {
            FitToThumbnail(1.0);
        }

        private void Menu_Resize_Half(object sender, EventArgs e) {
            FitToThumbnail(0.5);
        }

        private void Menu_Resize_Quarter(object sender, EventArgs e) {
            FitToThumbnail(0.25);
        }

        private void Menu_Resize_Fullscreen(object sender, EventArgs e) {
            IsFullscreen = true;
        }

        private void Menu_Position_TopLeft(object sender, EventArgs e) {
            var screen = Screen.FromControl(this);

            Location = new Point(
                screen.WorkingArea.Left - ChromeBorderHorizontal,
                screen.WorkingArea.Top - ChromeBorderVertical
            );
        }

        private void Menu_Position_TopRight(object sender, EventArgs e) {
            var screen = Screen.FromControl(this);

            Location = new Point(
                screen.WorkingArea.Width - Size.Width + ChromeBorderHorizontal,
                screen.WorkingArea.Top - ChromeBorderVertical
            );
        }

        private void Menu_Position_BottomLeft(object sender, EventArgs e) {
            var screen = Screen.FromControl(this);

            Location = new Point(
                screen.WorkingArea.Left - ChromeBorderHorizontal,
                screen.WorkingArea.Height - Size.Height + ChromeBorderVertical
            );
        }

        private void Menu_Position_BottomRight(object sender, EventArgs e) {
            var screen = Screen.FromControl(this);

            Location = new Point(
                screen.WorkingArea.Width - Size.Width + ChromeBorderHorizontal,
                screen.WorkingArea.Height - Size.Height + ChromeBorderVertical
            );
        }

        private void Menu_Reduce_click(object sender, EventArgs e) {
            //Hide form in a platform specific way
            Program.Platform.HideForm(this);
        }

        private void Menu_Chrome_click(object sender, EventArgs e) {
            if (FormBorderStyle == FormBorderStyle.Sizable) {
                FormBorderStyle = FormBorderStyle.None;
                Location = new Point {
                    X = Location.X + SystemInformation.FrameBorderSize.Width,
                    Y = Location.Y + SystemInformation.FrameBorderSize.Height
                };
            }
            else {
                FormBorderStyle = FormBorderStyle.Sizable;
                Location = new Point {
                    X = Location.X - SystemInformation.FrameBorderSize.Width,
                    Y = Location.Y - SystemInformation.FrameBorderSize.Height
                };
            }

            Invalidate();
        }

        private void Menu_Language_click(object sender, EventArgs e) {
            ToolStripItem tsi = (ToolStripItem)sender;

            string langCode = tsi.Tag as string;

            if (Program.ForceGlobalLanguageChange(langCode))
                this.Close();
            else
                MessageBox.Show("Error");
        }

        private void Menu_About_click(object sender, EventArgs e) {
            this.Hide();

            using (var box = new AboutForm()) {
                box.Location = RecenterLocation(this, box);
                box.ShowDialog();
                Location = RecenterLocation(box, this);
            }

            this.Show();
        }

        private void Menu_Close_click(object sender, EventArgs e) {
            this.Close();
        }

        private void Menu_Fullscreen_ExitFullscreen_click(object sender, EventArgs e) {
            IsFullscreen = false;
        }

    }
}