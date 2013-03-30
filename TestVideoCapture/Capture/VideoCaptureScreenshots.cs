using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DirectShowLib;

namespace TestVideoCapture.Capture
{
    public delegate void CaptureDone();


    public sealed class VideoCaptureScreenshots : VideoCaptureBase, ISampleGrabberCB
    {

        private readonly ISampleGrabber _iSampleGrabber = (ISampleGrabber)new SampleGrabber();
        private VideoInfoHeader _videoInfoHeader;

//        public event CaptureDone CaptureDoneEvent;
        public event PictureSaved PictureSavedEvent;

        

        private bool fCaptured = true;
        private byte[] _savedArray;

        readonly Control _invokerControl = new Control();

        public VideoCaptureScreenshots(string videoDeviceName, PhysicalConnectorType? connectorType) 
            : base(videoDeviceName, connectorType)
        {
            _invokerControl.CreateControl();
            BuildGraph();
            CreateVideoWindow();

            
        }

        protected override void BuildGraph()
        {
            //1. add video capture device
            IBaseFilter filterDocumentCameraDevice = null;

            int hr = Graph.AddSourceFilterForMoniker(CurrentVideoDevice.Mon, null, CurrentVideoDevice.DevicePath, out filterDocumentCameraDevice);
            CheckHr(hr, "Can't add document camera device");

            //Configure crossbar
            hr = FixCrossbarRouting(ref filterDocumentCameraDevice, ConnectorType);
            //no check is needed

            var smartTee = (IBaseFilter) new SmartTee();
            hr = Graph.AddFilter(smartTee, "smart tee");

            var pinSmartTeeIn = FindPin(smartTee, PinDirection.Input);
            var pinDocumentCameraOut = FindPin(filterDocumentCameraDevice, PinDirection.Output);

            //connect device and smart tee
            hr = Graph.Connect(pinDocumentCameraOut, pinSmartTeeIn);
            CheckHr(hr, "Can't connect document camera and smart tee");


            //Add SampleGrabber to take screenshots
            var filterSampleGrabber = (IBaseFilter)_iSampleGrabber;

            hr = ConfigureSampleGrabberInitial();
            CheckHr(hr, "Can't initially configure sample grabber");

            hr = Graph.AddFilter(filterSampleGrabber, "Sample Grabber");
            CheckHr(hr, "Can't add sample grabber to graph");

            //connect sample grabber and video device //add LAV decoder and connect 

            var pinSampleGrabberIn = FindPin(filterSampleGrabber, PinDirection.Input);
            var pinSmartTeeOutPreview = FindPin(smartTee, PinDirection.Output, "Preview");





            hr = Graph.Connect(pinSmartTeeOutPreview, pinSampleGrabberIn);
            CheckHr(hr, "Can't connect sample grabber and smart tee preview");

            //render output pin of sample grabber - for video preview
            var pinSampleGrabberOut = FindPin(filterSampleGrabber, PinDirection.Output);

            hr = Graph.Render(pinSampleGrabberOut);
            CheckHr(hr, "Can't render output pin of sample grabber");

            //render output pin for samplegrabber - previewing
            hr = ConfigureSampleGrabberFinal();
            CheckHr(hr, "Can't finally configure sample grabber");
            //stop building graph
        }

        /// <summary>
        /// Initial configuration of sample grabber - perform after sample grabber filter is created
        /// </summary>
        /// <returns></returns>
        private int ConfigureSampleGrabberInitial()
        {
            int hr;
            var mediaType = new AMMediaType
            {
                majorType = MediaType.Video,
                subType = MediaSubType.RGB24,
                formatType = FormatType.VideoInfo
            };
            hr = _iSampleGrabber.SetMediaType(mediaType);
            return hr;
        }

        /// <summary>
        /// Final sample grabber configuration - perform when graph is built and rendered
        /// </summary>
        private int ConfigureSampleGrabberFinal()
        {
            var mediaType = new AMMediaType();
            int hr = _iSampleGrabber.GetConnectedMediaType(mediaType);
            CheckHr(hr, "Can't get connected media type");
            _videoInfoHeader = (VideoInfoHeader)Marshal.PtrToStructure(mediaType.formatPtr, typeof(VideoInfoHeader));

            hr = _iSampleGrabber.SetBufferSamples(false);
            CheckHr(hr, "Can't set buffer samples false");
            hr = _iSampleGrabber.SetOneShot(false);
            CheckHr(hr, "Can't set one shot false for sample grabber");
            //callback to SampleCB
            hr = _iSampleGrabber.SetCallback(null, 0);
            return hr;
        }

        //this function does nothing 
        public int SampleCB(double sampleTime, IMediaSample pSample)
        {
            Thread.Sleep(50);
            return 0;
        }

        /// <summary>
        /// put data to a bit array
        /// </summary>
        /// <param name="sampleTime"></param>
        /// <param name="pBuffer"></param>
        /// <param name="bufferLen"></param>
        /// <returns></returns>
        public int BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen)
        {
            if (fCaptured || (_savedArray == null))
            {
                return 1;
            }

            fCaptured = true;

            if ((pBuffer != IntPtr.Zero) && (bufferLen > 1000) && (bufferLen <= _savedArray.Length))
            {
                Marshal.Copy(pBuffer, _savedArray, 0, bufferLen);

            }
            else
            {
                return 1;
            }


            _invokerControl.BeginInvoke(new CaptureDone(OnCaptureDone));


            return 0;
        }


        void OnCaptureDone()
        {
            try
            {
                if (_iSampleGrabber == null) return;
                //callback set to sampleCB thar does nothing
                int hr = _iSampleGrabber.SetCallback(null, 0);

                CheckHr(hr, "Can't set callback for sample grabber");

                int w = _videoInfoHeader.BmiHeader.Width;
                int h = _videoInfoHeader.BmiHeader.Height;

                if (((w & 0x03) != 0) || (w < 32) || (w > 4096) || (h < 32) || (h > 4096))
                    return;
                int stride = w * 3;

                GCHandle handle = GCHandle.Alloc(_savedArray, GCHandleType.Pinned);
                var scan0 = (int)handle.AddrOfPinnedObject();
                scan0 += (h - 1) * stride;

                var imageCaptured = new Bitmap(w, h, -stride, PixelFormat.Format24bppRgb, (IntPtr)scan0);

                PictureSavedEvent(this, new PictureSavedArgs { Created = DateTime.Now, Image = imageCaptured });

                handle.Free();
                _savedArray = null;


            }
            catch (Exception e)
            {
                MessageBox.Show(@"Couldn't grab image");
                throw;
            }
        }

        public delegate void PictureSaved(object sender, PictureSavedArgs args);

        public class PictureSavedArgs
        {
            public DateTime Created { get; set; }
            //public string FileName { get; set; }
            public Bitmap Image { get; set; }
        }

        public void Snapshot()
        {
            if (_iSampleGrabber == null) return;
            if (_savedArray == null)
            {
                int size = _videoInfoHeader.BmiHeader.ImageSize;
                if ((size < 1000) || (size > 16000000))
                {
                    return;
                }
                _savedArray = new byte[size + 64000];

            }
            //old image...

            fCaptured = false;
            //set callback to bufferCB
            int hr = _iSampleGrabber.SetCallback(this, 1);
            CheckHr(hr, "Can't set Callback");
        }
    }
}
