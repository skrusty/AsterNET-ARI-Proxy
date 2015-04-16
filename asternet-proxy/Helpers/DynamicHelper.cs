using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsterNET.ARI.Proxy.Helpers
{
	public class DynamicHelper
	{
		public static bool Exist(dynamic obj, string name)
		{
			return obj.GetType().GetProperty(name) != null;
		}

		public static object GetPropertyOrDefault(dynamic obj, string name)
		{
			if (Exist(obj, name))
				return obj.GetType().GetProperty(name).GetValue(obj, name);
			return null;
		}

		public static object GetProperty(dynamic obj, string name)
		{
			if (Exist(obj, name))
				return obj.GetType().GetProperty(name).GetValue(obj, name);
			throw new Exception("Property not found");
		}
	}
}
