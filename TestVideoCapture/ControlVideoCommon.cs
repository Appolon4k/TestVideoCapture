using System;
using System.Drawing;
using System.Windows.Forms;
using DirectShowLib;
using TestVideoCapture.Capture;

namespace TestVideoCapture
{
    public class ControlVideoCommon
    {
        public VideoCaptureBase Capture { get; private set; }

        //preview-capture control - no screenshot button
        public ControlVideoCommon(VideoCaptureBase capture, Panel videoPanel)
        {
            //creating videoCapture
            Capture = capture;
            _panel = videoPanel;
            CreateVideoWindow(_panel);
            Capture.StartGraph();
        }

        public void ClearControl()
        {
            if (Capture == null) return;
            Capture.StopGraph();
            Capture.Dispose();
            Capture = null;
        }

        private Panel _panel;

        public void CreateVideoWindow(Panel panel)
        {
            _panel = panel;
            int hr = Capture.VideoWindow.put_Owner(panel.Handle);
            if (hr != 0) throw new Exception("Can't set video window owner");

            Capture.VideoWindow.put_WindowStyle(WindowStyle.Child | WindowStyle.ClipChildren);
            Rectangle rc = panel.ClientRectangle;

            Capture.VideoWindow.SetWindowPosition(0, 0, rc.Right, rc.Bottom);
        }

        public void ResizeVideoWindow(double ratio=1.25)
        {
            IntPtr owner;
            Capture.VideoWindow.get_Owner(out owner);

            if (Capture.VideoWindow != null && owner == _panel.Handle)
            {
                Rectangle rc = _panel.ClientRectangle;

                var w = rc.Right;
                var h = rc.Bottom;

                int width;
                int top;
                int left;
                int height;

                CalculateCoordinates(w, h, out width, out top, out left, out height, ratio);
                Capture.VideoWindow.put_Visible(OABool.False);
                Capture.VideoWindow.SetWindowPosition(left, top, width, height);
                Capture.VideoWindow.put_Visible(OABool.True);
            }
        }

        private static void CalculateCoordinates(int w, int h, out int width, out int top, out int left, out int height, double ratio)
        {

            //TODO параметры взять из конструктора, так как камера может давать разрешение отличное от 4 на 3

            if ((double)w / (double)h <= ratio)
            {
                left = 0;
                width = w;
                height = (int)(1.0/ratio * width);
                top = (int)((h - height) / 2.0);
            }
            else
            {
                top = 0;
                height = h;
                width = (int)(ratio * height);
                left = (int)((w - width) / 2.0);
            }
        }
    }

}
