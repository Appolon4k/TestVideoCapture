using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using DirectShowLib;

namespace TestVideoCapture.Capture
{

    public struct DeviceSettings
    {
        public string DeviceName { get; set; }
        public bool IsOn { get; set; }
        public bool IsDocumentCamera { get; set; }
        public PhysicalConnectorType? ConnectorType { get; set; }
    }

    public static class VideoSettings
    {


        public static Dictionary<string, DeviceSettings> DeviceSettingsFile = new Dictionary<string, DeviceSettings>();
        public static Dictionary<string, DeviceSettings> DeviceSettingsCurrent = new Dictionary<string, DeviceSettings>();

        public static string FolderPath { get; set; }

     

        /// <summary>
        /// Check if devices in settings file correspond to plugged-in devices, and make changes if necessary
        /// </summary>
        /// <returns></returns>
        public static bool CheckIfConsistant()
        {
            ReadSettingsFromFile();
            ReadCurrentDevicesSettings();

            var consistant = true;

            for (int i = DeviceSettingsFile.Count - 1; i >= 0; i--)
            {
                var key = DeviceSettingsFile.ElementAt(i).Key;
                //if we have no current device in 
                if (DeviceSettingsCurrent.Keys.Contains(key)) continue;
                //remove device from settings file
                DeviceSettingsFile.Remove(key);
                consistant = false;
            }


            for (int i = DeviceSettingsCurrent.Count - 1; i >= 0; i--)
            {
                var key = DeviceSettingsCurrent.ElementAt(i).Key;

                //if we have a setting that is not a plugged-in device
                if (DeviceSettingsFile.Keys.Contains(key)) continue;
                var value = DeviceSettingsCurrent.ElementAt(i).Value;
                DeviceSettingsFile.Add(key, value);
                consistant = false;


            }

            //save file again if changes were made
            if (!consistant) SaveSettingsToFile();
            return false;
        }

        public static void ModifySetting(string key, DeviceSettings setting)
        {
            DeviceSettingsFile[key] = setting;
            SaveSettingsToFile();
        }

        public static void ModifyIsOn(string key, bool isOn)
        {
            var setting = new DeviceSettings
                              {
                                  ConnectorType = DeviceSettingsFile[key].ConnectorType,
                                  DeviceName = DeviceSettingsFile[key].DeviceName,
                                  IsDocumentCamera = DeviceSettingsFile[key].IsDocumentCamera,
                                  IsOn = DeviceSettingsFile[key].IsOn
                              };
            setting.IsOn = isOn;
            ModifySetting(key,setting);

        }

        public static void ModifyIsDocumentCamera(string key, bool isDocCam)
        {
            var setting = new DeviceSettings
            {
                ConnectorType = DeviceSettingsFile[key].ConnectorType,
                DeviceName = DeviceSettingsFile[key].DeviceName,
                IsDocumentCamera = DeviceSettingsFile[key].IsDocumentCamera,
                IsOn = DeviceSettingsFile[key].IsOn
            };
            setting.IsDocumentCamera = isDocCam;
            ModifySetting(key, setting);

        }

        public static void ModifyConnectorType(string key, PhysicalConnectorType? connectorType)
        {
            var setting = new DeviceSettings
                              {
                                  ConnectorType = DeviceSettingsFile[key].ConnectorType,
                                  DeviceName = DeviceSettingsFile[key].DeviceName,
                                  IsDocumentCamera = DeviceSettingsFile[key].IsDocumentCamera,
                                  IsOn = DeviceSettingsFile[key].IsOn
                              };
            
            setting.ConnectorType = connectorType;
            ModifySetting(key,setting);

        }
        //-------------------------------------------------------------------------------------------------------------------



        //-------------------------------------------------------------------------------------------------------------------
        public static void ReadCurrentDevicesSettings()
        {
            DeviceSettingsCurrent = new Dictionary<string, DeviceSettings>();

            var currentDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            foreach (var currentDevice in currentDevices)
            {
                

                var setting = new DeviceSettings
                {
                    DeviceName = currentDevice.Name,
                    IsOn = false,
                    IsDocumentCamera = false,
                    ConnectorType = PhysicalConnectorType.Video_SerialDigital
                };
                DeviceSettingsCurrent.Add(currentDevice.DevicePath, setting);
            }

            //DeviceSettings = CurrentDeviceSettings;
        }

        //---------------------------------------------------------------------------------------------------------------------

        public static void ReadSettingsFromFile()
        {
            if (!File.Exists("videosettings")) return;

            using (var reader = new StreamReader("videosettings"))
            {
                DeviceSettingsFile = new Dictionary<string, DeviceSettings>();
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;

                    var lineParts = line.Split(';');
                    //create new struct of settings

                    string nameKey = lineParts[0];

                    var setting = new DeviceSettings();

                    setting.DeviceName = lineParts[1];
                    setting.IsOn = bool.Parse(lineParts[2]);
                    setting.IsDocumentCamera = bool.Parse(lineParts[3]);
                    switch (lineParts[4])
                    {
                        case "Video_Composite":
                            setting.ConnectorType = PhysicalConnectorType.Video_Composite;
                            break;
                        case "Video_SerialDigital":
                            setting.ConnectorType = PhysicalConnectorType.Video_SerialDigital;
                            break;
                        case "Video_SVideo":
                            setting.ConnectorType = PhysicalConnectorType.Video_SVideo;
                            break;
                        default:
                            setting.ConnectorType = null;
                            break;
                    }

                    DeviceSettingsFile.Add(nameKey, setting);
                }
            }
        }

        public static void SaveSettingsToFile()
        {
            //file is rewritten
            using (var writer = new StreamWriter("videosettings", false))
            {
                foreach (var deviceSetting in DeviceSettingsFile)
                {
                    var sb = new StringBuilder();
                    sb.Append(deviceSetting.Key);
                    sb.Append(";");
                    sb.Append(deviceSetting.Value.DeviceName);
                    sb.Append(";");
                    sb.Append(deviceSetting.Value.IsOn);
                    sb.Append(";");
                    sb.Append(deviceSetting.Value.IsDocumentCamera);
                    sb.Append(";");
                    sb.Append(deviceSetting.Value.ConnectorType);

                    writer.WriteLine(sb.ToString());
                }
            }
        }



        public static void ShowCodecPropertiesPanel(IntPtr handle)
        {
            IBaseFilter captureFilter = null;
            var filterGraph2 = new FilterGraph() as IFilterGraph2;
            //obtain filter interface
            var compressorFilters = DsDevice.GetDevicesOfCat(FilterCategory.VideoCompressorCategory);
            foreach (var filter in compressorFilters)
            {
                if(filter.Name.ToLower().Contains(ConfigurationManager.AppSettings["codec"].ToLower()))
                {
                    //get filter 
                    if (filterGraph2 != null)
                        filterGraph2.AddSourceFilterForMoniker(filter.Mon, null, filter.Name, out captureFilter);
                    break;
                }
            }
            //--------------------------------------------------------------
            //we now have filter and need to show its VFW config dialog
            IAMVfwCompressDialogs dialogs;

            dialogs = captureFilter as IAMVfwCompressDialogs;

            if (dialogs != null) dialogs.ShowDialog(VfwCompressDialogs.Config, handle);
        }


//        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
//        public static extern int OleCreatePropertyFrame(
//            [In] IntPtr hwndOwner,
//            [In] int x,
//            [In] int y,
//            [In, MarshalAs(UnmanagedType.LPWStr)] string lpszCaption,
//            [In] int cObjects,
//            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.IUnknown)] object[] ppUnk,
//            [In] int cPages,
//            [In] IntPtr pPageClsID,
//            [In] int lcid,
//            [In] int dwReserved,
//            [In] IntPtr pvReserved
//            );
//
//
//
//        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
//        public static void ShowFilterPropertyPage(IBaseFilter filter, IntPtr parent)
//        {
//            int hr = 0;
//            FilterInfo filterInfo;
//            DsCAUUID caGuid;
//            object[] objs;
//
//            if (filter == null)
//                throw new ArgumentNullException("filter");
//
//            if (HasPropertyPages(filter))
//            {
//                hr = filter.QueryFilterInfo(out filterInfo);
//                DsError.ThrowExceptionForHR(hr);
//
//                if (filterInfo.pGraph != null)
//                    Marshal.ReleaseComObject(filterInfo.pGraph);
//
//                hr = (filter as ISpecifyPropertyPages).GetPages(out caGuid);
//                DsError.ThrowExceptionForHR(hr);
//
//                try
//                {
//                    objs = new object[1];
//                    objs[0] = filter;
//
//                    hr = OleCreatePropertyFrame(
//                        parent, 0, 0,
//                        filterInfo.achName,
//                        objs.Length, objs,
//                        caGuid.cElems, caGuid.pElems,
//                        0, 0,
//                        IntPtr.Zero
//                        );
//                    DsError.ThrowExceptionForHR(hr);
//                }
//                finally
//                {
//                    Marshal.FreeCoTaskMem(caGuid.pElems);
//                }
//            }
//        }
//
//        public static bool HasPropertyPages(IBaseFilter filter)
//        {
//            if (filter == null)
//                throw new ArgumentNullException("filter");
//
//            return ((filter as ISpecifyPropertyPages) != null);
//        }

     

    }
}
