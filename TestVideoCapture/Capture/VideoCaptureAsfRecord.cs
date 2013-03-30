using System;
using System.Configuration;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DirectShowLib;

namespace TestVideoCapture.Capture
{
    public sealed class VideoCaptureAsfRecord : VideoCaptureBase
    {
        private string FileName { get; set; }
        private string AudioDeviceName { get; set; }
        private int CaptureWidth { get; set; }
        private int CaptureHeight { get; set; }


        /// <summary>
        /// Capturing video to a file and displaying preview in the window
        /// </summary>
        /// <param name="videoDevicePath">Device to capture video</param>
        /// <param name="audioDeviceName">Microphone to capture sound, part of the name (case insensitive) (e.g. "realtek")</param>
        /// <param name="connectorType">Connector type - such as SVideo or Composite...</param>
        /// <param name="fileName">Name of the file to which we capture (here - mpeg4) - with extension. For example test.mp4</param>
        /// <param name="captureWidth"> </param>
        /// <param name="captureHeight"> </param>
        public VideoCaptureAsfRecord(string videoDevicePath, string audioDeviceName, PhysicalConnectorType? connectorType, string fileName, int captureWidth, int captureHeight) 
            : base(videoDevicePath, connectorType)
        {
            FileName = fileName;
            AudioDeviceName = audioDeviceName;
            CaptureWidth = captureWidth;
            CaptureHeight = captureHeight;
            BuildGraph();
            CreateVideoWindow();
        }

        #region Overrides of VideoCaptureBase

        protected override void BuildGraph()
        {
            
            //add capture device to graph
            IBaseFilter videoDeviceFilter;
            int hr = Graph.AddSourceFilterForMoniker(CurrentVideoDevice.Mon, null, CurrentVideoDevice.Name, out videoDeviceFilter);
            CheckHr(hr, "Can't add capture device to graph");

            //add crossbar for capture device if it exists
            hr = FixCrossbarRouting(ref videoDeviceFilter, ConnectorType);
            //CheckHr(hr, "Can't fix crossbar routing");

            //add smart tee - splits videostream to two parts
            var smartTee = (IBaseFilter)new SmartTee();
            hr = Graph.AddFilter(smartTee, "Smart Tee");
            CheckHr(hr, "Can't add Smart Tee");

            //connect capture filter to smart tee 
            var pinCaptureOut = FindPin(videoDeviceFilter, PinDirection.Output, "Запись");
            var pinTeeIn = FindPin(smartTee, PinDirection.Input);
            
            //configuring framerate / resolution on capture pin
            ConfigureResolution(CaptureGraphBuilder,videoDeviceFilter);
            
            hr = Graph.Connect(pinCaptureOut, pinTeeIn);
            CheckHr(hr, "Can't connect Capture device and Smart Tee");



            var asfWriter = new WMAsfWriter();
            var asfWriterF = (IBaseFilter) asfWriter;



            hr =  Graph.AddFilter(asfWriterF, "ASF Writer");
            CheckHr(hr, "Can't add ASF Writer");
            //configure asf writer with a profile

            var filePath = ConfigurationManager.AppSettings["qualityFilePath"];

            ConfigProfileFromFile(asfWriterF, filePath);

            //CONNECT SMART TEE TO ASF WRITER

            var pinAsfWriterVideoIn = FindPin(asfWriterF, PinDirection.Input, "Video");
            var smartTeeOutCapture = FindPin(smartTee, PinDirection.Output, "Capture");
            hr = Graph.Connect(smartTeeOutCapture, pinAsfWriterVideoIn);
            CheckHr(hr, "Can't connect smart tee and asf writer");
            
            IBaseFilter audioCaptureFilter = null;

            var soundCaptureDevice = GetFilterFromCategoryByName(FilterCategory.AudioInputDevice,AudioDeviceName);
            bool useAudio = false;
            if(soundCaptureDevice!=null)
            {
                useAudio = true;
                hr = Graph.AddSourceFilterForMoniker(soundCaptureDevice.Mon, null, soundCaptureDevice.Name,
                                                     out audioCaptureFilter);
                CheckHr(hr, "Can't add sound capture filter");
            }

            if (!useAudio) MessageBox.Show(@"Устройство записи, указанное в конфигурационном файле не найдено. Запись будет вестись без звука", @"Предупреждение", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            IBaseFilter audioCompressorFilter = null;

            if (useAudio)
            {
                //connect sound capture device and ASF writer
                var pinAudioCaptureDeviceOut = FindPin(audioCaptureFilter, PinDirection.Output);
                var pinAsfWriterIn = FindPin(asfWriterF,PinDirection.Input,"Audio");
                hr = Graph.Connect(pinAudioCaptureDeviceOut, pinAsfWriterIn);
                CheckHr(hr, "Can't connect audio capture device and asf writer");
            }



            //render tee preview pin for generating output window
            var pinTeeOutPreview = FindPin(smartTee, PinDirection.Output, "Preview");
            hr = Graph.Render(pinTeeOutPreview);
            CheckHr(hr, "Can't render Tee Preview Pin");

            //output file name 
            var asfSink = asfWriter as IFileSinkFilter;
            if(asfSink == null) CheckHr((int)-1, "Can't get IFileSinkWriter");
            hr = asfSink.SetFileName(FileName,null);
            CheckHr(hr,"Can't set fileName");


        }

        #endregion

        /// <summary>
        /// Configure profile from file to Asf file writer
        /// </summary>
        /// <param name="asfWriter"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        private void ConfigProfileFromFile(IBaseFilter asfWriter, string filename)
        {
            int hr;
            //string profilePath = "test.prx";
            // Set the profile to be used for conversion




            if ((filename != null) && (File.Exists(filename)))
            {
                // Load the profile XML contents
                string profileData;
                using (StreamReader reader = new StreamReader(File.OpenRead(filename)))
                {
                    profileData = reader.ReadToEnd();
                }

                // Create an appropriate IWMProfile from the data
                // Open the profile manager
                IWMProfileManager profileManager;
                IWMProfile wmProfile = null;
                hr = WMLib.WMCreateProfileManager(out profileManager);
                if (hr >= 0)
                {
                    // error message: The profile is invalid (0xC00D0BC6)
                    // E.g. no <prx> tags
                    hr = profileManager.LoadProfileByData(profileData, out wmProfile);
                }

                if (profileManager != null)
                {
                    Marshal.ReleaseComObject(profileManager);
                    profileManager = null;
                }

                // Config only if there is a profile retrieved
                if (hr >= 0)
                {
                    // Set the profile on the writer
                    IConfigAsfWriter configWriter = (IConfigAsfWriter)asfWriter;
                    hr = configWriter.ConfigureFilterUsingProfile(wmProfile);
                    if (hr >= 0)
                    {
                        return;
                    }
                }
            }
            return;
        }

        private void ConfigureFramerate(ICaptureGraphBuilder2 captureGraph, IBaseFilter captureFilter, double framerate)
        {
            object streamConfigObject;

            int hr = captureGraph.FindInterface(PinCategory.Capture, MediaType.Video, captureFilter, typeof(IAMStreamConfig).GUID,
                                                out streamConfigObject);
            CheckHr(hr, "Can't find interface");
            if (streamConfigObject == null) throw new Exception("Cannot set Stream Configuration");
            var streamConfig = (IAMStreamConfig)streamConfigObject;

            AMMediaType mediaType;

            streamConfig.GetFormat(out mediaType);
            var infoHeader = (VideoInfoHeader)Marshal.PtrToStructure(mediaType.formatPtr, typeof(VideoInfoHeader));


            //change frame rate
            infoHeader.AvgTimePerFrame = (long)(10000000 / framerate);
            




            //copy media structure back
            Marshal.StructureToPtr(infoHeader, mediaType.formatPtr, false);
            streamConfig.SetFormat(mediaType);
        }

        private void ConfigureResolution(ICaptureGraphBuilder2 captureGraph, IBaseFilter captureFilter)
        {
            object streamConfigObject;

            int hr = captureGraph.FindInterface(PinCategory.Capture, MediaType.Video, captureFilter, typeof(IAMStreamConfig).GUID,
                                                out streamConfigObject);
            CheckHr(hr, "Can't find interface");
            if (streamConfigObject == null) throw new Exception("Cannot set Stream Configuration");
            var streamConfig = (IAMStreamConfig)streamConfigObject;

            AMMediaType mediaType;

            streamConfig.GetFormat(out mediaType);
            var infoHeader = (VideoInfoHeader)Marshal.PtrToStructure(mediaType.formatPtr, typeof(VideoInfoHeader));

            infoHeader.BmiHeader.Width = CaptureWidth;
            infoHeader.BmiHeader.Height = CaptureHeight;


            //copy media structure back
            Marshal.StructureToPtr(infoHeader, mediaType.formatPtr, false);
            streamConfig.SetFormat(mediaType);
        }


    }
}
