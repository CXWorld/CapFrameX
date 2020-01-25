using System;
using System.Collections.Generic;
using System.Linq;
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
			return propertyDataValue.TrimEnd();
		}

		/// <summary>
		/// Gets the vendor name
		/// </summary>
		/// <returns>"NVIDIA", "Advanced Micro Devices, Inc."</returns>
		public static string GetGraphicCardVendor()
		{
			string propertyDataValue = string.Empty;
			const string propertyDataName = "AdapterCompatibility";

			var win32DeviceClassName = "Win32_VideoController";
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
			var moduleSetting = new Dictionary<long, int>();

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
								{
									var currentCapacity = Convert.ToInt64(value);
									if (moduleSetting.ContainsKey(currentCapacity))
										moduleSetting[currentCapacity]++;
									else
										moduleSetting.Add(currentCapacity, 1);
								}
							}
						}
					}
				}
			}
			catch { propertyDataValueSpeed = "unknown"; moduleSetting.Add(0, 1); }

			if (!moduleSetting.Any())
				moduleSetting.Add(0, 0);

			//RAM size + speed
			// example: 48GB (4x4GB+4x8GB)
			var infoString = string.Empty;
			long wholeCapacity = 0;
			foreach (var item in moduleSetting)
			{
				wholeCapacity += item.Value * item.Key;
				infoString += $"{item.Value}x{item.Key / ONE_GIB}GB+";
			}

			return $"{wholeCapacity / ONE_GIB}GB ({infoString.Remove(infoString.Length - 1)}) {propertyDataValueSpeed}MT/s";
		}

		public static string GetGraphicDriverVersion()
		{
			return "unknown";
		}
	}
}
