using DirectShowLib;

namespace TestVideoCapture.Capture
{
    public sealed class VideoCapturePreview : VideoCaptureBase
    {
        public VideoCapturePreview(string videoDeviceName, PhysicalConnectorType? connectorType) : 
            base(videoDeviceName, connectorType)
        {
            BuildGraph();
            CreateVideoWindow();
        }

        protected override void BuildGraph()
        {
            IBaseFilter filterVideoDevice;
            int hr = Graph.AddSourceFilterForMoniker(CurrentVideoDevice.Mon, null, CurrentVideoDevice.Name, out filterVideoDevice);
            CheckHr(hr, "Can't add video device");

            hr = FixCrossbarRouting(ref filterVideoDevice, ConnectorType);
            
            var smartTee = (IBaseFilter)new SmartTee();
            hr = Graph.AddFilter(smartTee, "Smart Tee");
            CheckHr(hr, "Can't add Smart Tee");

            var pinCaptureOut = FindPin(filterVideoDevice, PinDirection.Output, "Запись");
            var pinTeeIn = FindPin(smartTee, PinDirection.Input);
            hr = Graph.Connect(pinCaptureOut, pinTeeIn);
            CheckHr(hr, "Can't connect Capture device and Smart Tee");

            var pinTeeOutPreview = FindPin(smartTee, PinDirection.Output, "Preview");
            hr = Graph.Render(pinTeeOutPreview);
            CheckHr(hr, "Can't render Tee Preview Pin");
        }

    }
}
