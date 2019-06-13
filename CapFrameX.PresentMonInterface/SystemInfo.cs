using System;
using System.Management;

namespace CapFrameX.PresentMonInterface
{
    public static class SystemInfo
    {
        private static readonly long ONE_GIB = 1073741824;

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
            const string propertyDataNameCapacity = "Capacity";
            string propertyDataValueSpeed = "unknown";
            const string propertyDataNameSpeed = "Speed";

            var win32DeviceClassName = "Win32_PhysicalMemory";
            var query = string.Format("select * from {0}", win32DeviceClassName);
            long capacitySum = 0;

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
                            if (propertyDataNameSpeed == propertyData.Name)
                            {
                                var value = propertyData.Value;

                                if (value != null)
                                    propertyDataValueSpeed = value.ToString();
                            }

                            if (propertyDataNameCapacity == propertyData.Name)
                            {
                                var value = propertyData.Value;

                                if (value != null)
                                    capacitySum += Convert.ToInt64(value);
                            }
                        }
                    }
                }
            }
            catch { propertyDataValueSpeed = "unknown"; capacitySum = 0; }

            //RAM size + speed
            return $"{capacitySum/ ONE_GIB} GB {propertyDataValueSpeed} MT/s";
        }
    }
}
