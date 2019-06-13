using System;
using System.Management;

namespace CapFrameX.PresentMonInterface
{
    public static class SystemInfo
    {
        public static string GetProcessorName()
        {
            string propertyDataValue = string.Empty;
            const string propertyDataName = "Name";

            var win32DeviceClassName = "win32_processor";
            var query = string.Format("select * from {0}", win32DeviceClassName);

            try
            {
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {
                            if (propertyData.Name == propertyDataName)
                            {
                                propertyDataValue = (string)propertyData.Value;
                                break;
                            }
                        }
                    }
                }
            }
            catch { propertyDataValue = string.Empty; }

            // Name
            return propertyDataValue;
        }

        public static string GetGraphicCardName()
        {
            string propertyDataValue = string.Empty;
            const string propertyDataName = "DeviceName";

            var win32DeviceClassName = "Win32_DisplayConfiguration";
            var query = string.Format("select * from {0}", win32DeviceClassName);

            try
            {
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {
                            if (propertyData.Name == propertyDataName)
                            {
                                propertyDataValue = (string)propertyData.Value;
                                break;
                            }
                        }
                    }
                }
            }
            catch { propertyDataValue = string.Empty; }

            //DeviceName
            return propertyDataValue;
        }

        public static string GetOSVersion()
        {
            string propertyDataValueCaption = string.Empty;
            const string propertyDataNameCaption = "Caption";
            string propertyDataValueBuildNumber = string.Empty;
            const string propertyDataNameBuildNumber = "BuildNumber";

            var win32DeviceClassName = "Win32_OperatingSystem";
            var query = string.Format("select * from {0}", win32DeviceClassName);

            try
            {
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {
                            if (propertyData.Name == propertyDataNameCaption)
                            {
                                propertyDataValueCaption = (string)propertyData.Value;
                            }

                            if (propertyData.Name == propertyDataNameBuildNumber)
                            {
                                propertyDataValueBuildNumber = (string)propertyData.Value;

                            }
                        }
                    }
                }
            }
            catch { propertyDataValueCaption = "Windows OS"; }

            return $"{propertyDataValueCaption} Build {propertyDataValueBuildNumber}";
        }

        public static string GetMotherboardName()
        {
            string propertyDataValueManufacturer = string.Empty;
            const string propertyDataNameManufacturer = "Manufacturer";
            string propertyDataValueProduct = string.Empty;
            const string propertyDataNameProduct = "Product";

            var win32DeviceClassName = "Win32_BaseBoard";
            var query = string.Format("select * from {0}", win32DeviceClassName);

            try
            {
                //Manufacturer + Product
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {
                            if (propertyData.Name == propertyDataNameManufacturer)
                            {
                                propertyDataValueManufacturer = (string)propertyData.Value;
                            }

                            if (propertyData.Name == propertyDataNameProduct)
                            {
                                propertyDataValueProduct = (string)propertyData.Value;

                            }
                        }
                    }
                }
            }
            catch { propertyDataValueManufacturer = string.Empty; propertyDataValueProduct = string.Empty; }

            //Manufacturer + Product
            string result = $"{propertyDataValueManufacturer} {propertyDataValueProduct}";
            return result.Replace(",", "");
        }

        public static string GetSystemRAMInfoName()
        {
            string propertyDataValueRAMSize = string.Empty;
            const string propertyDataNameFormFactor = "FormFactor";
            string propertyDataValueSpeed = string.Empty;
            const string propertyDataNameSpeed = "Speed";

            var win32DeviceClassName = "Win32_PhysicalMemory";
            var query = string.Format("select * from {0}", win32DeviceClassName);
            int formFactorSum = 0;

            string test = string.Empty;

            try
            {
                //Manufacturer + Product
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {

                            test += $"Name:{propertyData.Name} with value: {propertyData.Value?.ToString()}";
                            test += Environment.NewLine;
                            //if (propertyDataNameSpeed == propertyData.Name)
                            //{
                            //    var value = propertyData.Value;

                            //    if (value != null)
                            //        propertyDataValueSpeed = value.ToString();
                            //}

                            //if (propertyDataNameFormFactor == propertyData.Name)
                            //{
                            //    var value = propertyData.Value;

                            //    if (value != null)
                            //        formFactorSum += Convert.ToInt32(value);
                            //}
                        }
                    }
                }
            }
            catch { propertyDataValueSpeed = string.Empty; formFactorSum = 0; }

            //RAM size + speed
            return test; //$"{formFactorSum} GB {propertyDataValueSpeed} MT/s";
        }
    }
}
