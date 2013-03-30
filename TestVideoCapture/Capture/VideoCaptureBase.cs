using System;
using System.Linq;
using System.Windows.Forms;
using DirectShowLib;

namespace TestVideoCapture.Capture
{
    public abstract class VideoCaptureBase : IDisposable
    {
        //filter graph - main part 
        protected IFilterGraph2 Graph;      
        //media control to perform graph operations - to start, stop, pause, unpause graphs
        private readonly IMediaControl _mediaControl;
        //videoWindow - is it necessary
        public IVideoWindow VideoWindow;
        //used in setting filter graph, videostream configuration etc.
        protected ICaptureGraphBuilder2 CaptureGraphBuilder;
        protected PhysicalConnectorType ConnectorType;

        protected DsDevice CurrentVideoDevice;



        /// <summary>
        ///device name, may be equal
        /// </summary>
        public string VideoDeviceName { get; protected set; }
        
        /// <summary>
        ///full path to device - to distingish between similar devices 
        /// </summary>
        public string VideoDevicePath { get; protected set; }



        //CONSTRUCTOR
        protected VideoCaptureBase(string videoDevicePath, PhysicalConnectorType? connectorType = null)
        {
            Graph = (IFilterGraph2) new FilterGraph();

            if (connectorType != null) ConnectorType = (PhysicalConnectorType) connectorType;

            CaptureGraphBuilder = (ICaptureGraphBuilder2) new CaptureGraphBuilder2();

            int result = CaptureGraphBuilder.SetFiltergraph(Graph);
            if(result!=0) DsError.ThrowExceptionForHR(result);

            _mediaControl = (IMediaControl) Graph;
            GetCurrentVideoDevice(videoDevicePath);
        }

        protected VideoCaptureBase()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get 
        /// </summary>
        /// <param name="videoDevicePath"></param>
        protected void GetCurrentVideoDevice(string videoDevicePath)
        {
            if (videoDevicePath == null) return;
            CurrentVideoDevice = GetDeviceFromCategoryByPath(FilterCategory.VideoInputDevice, videoDevicePath);

            VideoDeviceName = CurrentVideoDevice.Name;
            VideoDevicePath = CurrentVideoDevice.DevicePath;
        }

        /// <summary>
        /// Add filters to graph and render stream
        /// </summary>
        protected abstract void BuildGraph();


        protected virtual void CreateVideoWindow()
        {
            if(VideoWindow!=null)
            {
                DisposeVideoWindow();    
            }
            VideoWindow = (IVideoWindow) Graph;
            if(VideoWindow==null)
            {
                throw new Exception("Can't create video window for the graph");
            }
        }

        /// <summary>
        /// Get one of several devices/filters in specified category by name 
        /// </summary>
        /// <param name="category">category guid, e.g. FilterCategory.AudioCompressorCategory</param>
        /// <param name="deviceName">part of the filter name (case insensitive)</param>
        /// <returns></returns>
        protected DsDevice GetFilterFromCategoryByName(Guid category, string deviceName)
        {
            var filters = DsDevice.GetDevicesOfCat(category);
            return filters.FirstOrDefault(filter => filter.Name.ToLower().Contains(deviceName.ToLower()));
        }

        /// <summary>
        /// Get one of several devices/filters in specified category by path - used when several devices have the same name, but different paths
        /// </summary>
        /// <param name="category">category guid, e.g. FilterCategory.AudioCompressorCategory</param>
        /// <param name="devicePath">part of the filter path (case insensitive)</param>
        /// <returns></returns>
        protected DsDevice GetDeviceFromCategoryByPath(Guid category, string devicePath)
        {
            var filters = DsDevice.GetDevicesOfCat(category);
            return filters.FirstOrDefault(filter => filter.DevicePath.ToLower().Contains(devicePath.ToLower()));
        }

        /// <summary>
        /// Configure crossbar inputs and connect crossbar to input device
        /// </summary>
        /// <param name="captureFilter"></param>
        /// <param name="physicalConnectorType"></param>
        /// <returns></returns>
        protected int FixCrossbarRouting(ref IBaseFilter captureFilter, PhysicalConnectorType? physicalConnectorType)
        {
            object obj = null;
            //fixing crossbar routing
            int hr = CaptureGraphBuilder.FindInterface(FindDirection.UpstreamOnly, null, captureFilter,
                                                typeof(DirectShowLib.IAMCrossbar).GUID, out obj);
            if (hr == 0 && obj != null)
            {
                //found something, check if it is a crossbar
                var crossbar = obj as IAMCrossbar;
                if (crossbar == null)
                    throw new Exception("Crossbar object has not been created");

                int numOutPin;
                int numInPin;
                crossbar.get_PinCounts(out numOutPin, out numInPin);

                //for all output pins
                for (int iOut = 0; iOut < numOutPin; iOut++)
                {
                    int pinIndexRelatedOut;
                    PhysicalConnectorType physicalConnectorTypeOut;
                    crossbar.get_CrossbarPinInfo(false, iOut, out pinIndexRelatedOut, out physicalConnectorTypeOut);

                    //for all input pins
                    for (int iIn = 0; iIn < numInPin; iIn++)
                    {
                        // check if we can make a connection between the input pin -> output pin
                        hr = crossbar.CanRoute(iOut, iIn);
                        if (hr == 0)
                        {
                            //it is possible, get input pin info
                            int pinIndexRelatedIn;
                            PhysicalConnectorType physicalConnectorTypeIn;
                            crossbar.get_CrossbarPinInfo(true, iIn, out pinIndexRelatedIn, out physicalConnectorTypeIn);

                            //bool indication if current input oin can be connected to output pin
                            bool canRoute = physicalConnectorTypeIn == physicalConnectorType;

                            //get video from composite channel (CVBS)
                            //should output pin be connected to current input pin
                            if (canRoute)
                            {
                                //connect input pin to output pin
                                hr = crossbar.Route(iOut, iIn);
                                if (hr != 0) throw new Exception("Output and input pins cannot be connected");
                            }
                        } //if(hr==0)
                    } //for(iIn...)
                } //for(iOut...)
            } //if(hr==0 && obj!=null)
            return hr;
        }

        /// <summary>
        /// Search filters by several names that may be either in English or in National Language
        /// Tries to find pin with one of the specified names
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="pinDirection"></param>
        /// <param name="pinName1">Obligatory pin name</param>
        /// <param name="pinName2">Optional pin name (e g Russian)</param>
        protected IPin FindPin(IBaseFilter filter, PinDirection pinDirection, string pinName1 = null, string pinName2 = null)
        {
            IPin pin = null;
            IEnumPins enumPins;
            var ipPins = new IPin[2];

            filter.EnumPins(out enumPins);
            enumPins.Reset();

            const int pcFetched = 0;

            //first = searching by name
            while (enumPins.Next(1, ipPins, (IntPtr)pcFetched) == 0)
            {
                string sPinName;
                ipPins[0].QueryId(out sPinName);
                PinDirection pinDir;
                ipPins[0].QueryDirection(out pinDir);
                PinInfo pinInfo;
                ipPins[0].QueryPinInfo(out pinInfo);

                if (pinDir != pinDirection)
                    continue;

                if (pinName1 == null)//return first found with searched direction if no name was specified
                {
                    pin = ipPins[0];
                    return pin;
                }

                if (sPinName.Contains(pinName1) || (pinName2 != null && sPinName.Contains((pinName2))))
                {
                    pin = ipPins[0];
                    return pin;
                }
            }

            //if no name was found - return first with appropriate pin direction
            enumPins.Reset();
            while (enumPins.Next(1, ipPins, (IntPtr)pcFetched) == 0)
            {
                PinDirection pinDir;
                ipPins[0].QueryDirection(out pinDir);
                if (pinDir == pinDirection)
                {
                    pin = ipPins[0];
                    return pin;
                }
            }

            //if no pin was returnned
            throw new Exception("No pin with such parameters found");
        }

        /// <summary>
        /// Checking error code, throw exception and show messagebox if error
        /// </summary>
        /// <param name="hr">result code</param>
        /// <param name="message">message to display</param>
        protected void CheckHr(int hr, string message)
        {
            if (hr < 0)
            {
                //note is it really needed??
                MessageBox.Show(message);
                DsError.ThrowExceptionForHR(hr);
            }
        }


        #region Implementation of IDisposable

        public virtual void Dispose()
        {
            DisposeVideoWindow();
            DisposeFilters();
            if(CurrentVideoDevice!=null) CurrentVideoDevice.Dispose();
        }

        /// <summary>
        /// Detach window and release window resources
        /// </summary>
        private void DisposeVideoWindow()
        {
            if (VideoWindow != null)
            {
                VideoWindow.put_Visible(OABool.False);
                VideoWindow.put_Owner(IntPtr.Zero);
                VideoWindow = null;
            }
        }

        /// <summary>
        /// Detach filters from graph and remove them
        /// </summary>
        protected virtual void DisposeFilters()
        {
            if (Graph == null) return;
            IEnumFilters ef;
            var f = new IBaseFilter[1];
            int hr = Graph.EnumFilters(out ef);
            if (hr == 0)
            {
                while (0 == ef.Next(1, f, IntPtr.Zero))
                {
                    Graph.RemoveFilter(f[0]);
                    ef.Reset();
                }
            }
            Graph = null;
        }

        #endregion

        #region Graph Operations

        public enum GraphState
        {
            Stopped,
            Paused,
            Running
        }
        public GraphState State { get; private set; }


        //TODO errors found - modify and edit code

        public void StartGraph()
        {

            if (_mediaControl == null) throw new Exception("No media control to start");

            if (State == GraphState.Stopped)
            {
                int hr = _mediaControl.Run();
                CheckHr(hr, "Error starting graph");
                State = GraphState.Running;
            }
            else throw new Exception("Graph is already running");
        }

        public void StopGraph()
        {
            if (_mediaControl == null) throw new Exception("No media control to stop");

            if (State != GraphState.Stopped)
            {
                int hr = _mediaControl.Stop();
                CheckHr(hr, "Error stopping graph");
                State = GraphState.Stopped;
            }
            else throw new Exception("Graph is already stopped");
        }

        public void PauseGraph()
        {
            if (_mediaControl == null) throw new Exception("No media control to pause");
            if (State == GraphState.Running)
            {
                int hr = _mediaControl.Pause();
                CheckHr(hr, "Error pausing graph");
                State = GraphState.Paused;
            }
            else throw new Exception("Graph is not running to be paused");
        }

        public void UnpauseGraph()
        {
            if (_mediaControl == null) throw new Exception("No media control to pause");
            if (State == GraphState.Paused)
            {
                int hr = _mediaControl.Run();
                CheckHr(hr, "Error unpausing graph");
                State = GraphState.Running;
            }
            else throw new Exception("Graph is not paused to be unpaused");

        }

        #endregion Graph Operations
    }
}


