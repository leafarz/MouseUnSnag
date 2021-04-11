using MouseUnSnag.Event;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MouseUnSnag
{
    public partial class MouseUnSnagForm : Form
    {
        public EventHandler<CustomEvent<bool>> onToggleWrap;

        private bool _allowVisible = false;

        public MouseUnSnagForm()
        {
            InitializeComponent();

            notifyIcon1.Icon = SystemIcons.Application;
            notifyIcon1.BalloonTipText = "MouseUnSnag is running in system tray";
            notifyIcon1.Visible = true;
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!_allowVisible)
            {
                value = false;
                if (!this.IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var item = e.ClickedItem as ToolStripMenuItem;
            switch (item.Text)
            {
                case "Toggle Wrap":
                    item.Checked = !item.Checked;
                    onToggleWrap?.Invoke(this, new CustomEvent<bool> { Payload = item.Checked });
                    break;

                case "Github":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/dale-roberts/MouseUnSnag/",
                        UseShellExecute = true
                    });
                    break;

                case "Exit":
                    this.Close();
                    break;

                default:
                    break;
            }
        }
    }
}
