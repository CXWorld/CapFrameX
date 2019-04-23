using System.Management;

namespace CapFrameX.PresentMonInterface
{
	public static class HardwareInfo
	{
		public static string GetProcessorName()
		{
			string propertyDataValue = string.Empty;
			const string propertyDataName = "Name";

			var win32DeviceClassName = "win32_processor";
			var query = string.Format("select * from {0}", win32DeviceClassName);

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
			// Name
			return propertyDataValue;
		}

		public static string GetGraphicCardName()
		{
			string propertyDataValue = string.Empty;
			const string propertyDataName = "DeviceName";

			var win32DeviceClassName = "Win32_DisplayConfiguration";
			var query = string.Format("select * from {0}", win32DeviceClassName);

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
			//DeviceName
			return propertyDataValue;
		}

		public static string GetMotherboardName()
		{
			string propertyDataValueManufacturer = string.Empty;
			const string propertyDataNameManufacturer = "Manufacturer";
			string propertyDataValueProduct = string.Empty;
			const string propertyDataNameProduct = "Product";

			var win32DeviceClassName = "Win32_BaseBoard";
			var query = string.Format("select * from {0}", win32DeviceClassName);

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
			//Manufacturer + Product
			return $"{propertyDataValueManufacturer} {propertyDataValueProduct}";
		}
	}
}
