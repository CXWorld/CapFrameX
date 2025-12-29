namespace CapFrameX.Service.Overlay;

/// <summary>
/// Hidden form used for provider detection by OverlayEditor.
/// The window name is used by OverlayEditor to detect if the provider is running.
/// </summary>
internal class ConnectForm : Form
{
    public ConnectForm(string windowName)
    {
        Text = windowName;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        Size = new Size(0, 0);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // Make window tool window (no taskbar entry)
            cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
            return cp;
        }
    }
}
