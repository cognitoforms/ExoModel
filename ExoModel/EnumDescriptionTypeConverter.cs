using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace ExoModel
{
	public class EnumDescriptionTypeConverter<T> : TypeConverter where T : struct
	{
		private readonly Dictionary<T, string> enumValueToStringMap = new Dictionary<T, string>();
		private readonly Dictionary<string, T> stringToEnumValueMap = new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase);

		public EnumDescriptionTypeConverter()
		{
			if (!typeof(T).IsEnum)
				throw new InvalidOperationException("Type is not Enum");

			if (typeof(T).IsDefined(typeof(FlagsAttribute), false))
				throw new InvalidOperationException("Flags Attribute not supported");

			// This allows us to also parse the original enum values also.
			foreach (var enumValue in (T[])Enum.GetValues(typeof(T)))
			{
				stringToEnumValueMap[enumValue.ToString()] = enumValue;
			}

			foreach (var mapping in GetEnumMappingsFromDescriptionAttribute())
			{
				enumValueToStringMap[mapping.Key] = mapping.Value;
				stringToEnumValueMap[mapping.Value] = mapping.Key;
			}
		}

		/// <summary>
		/// Return a collection of key value pairs describing the enum
		/// Key will be the Enum value
		/// Value will be the description from the DescriptionAttribute, or standard ToString if not present.
		/// </summary>
		/// <returns></returns>
		private static IEnumerable<KeyValuePair<T, string>> GetEnumMappingsFromDescriptionAttribute()
		{
			return (from value in (T[])Enum.GetValues(typeof(T))
					let field = typeof(T).GetField(value.ToString())
					let attribute = (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false)
					let description = attribute.Length == 1 ? attribute[0].Description : value.ToString()
					select new { value, description }).ToDictionary(t => t.value, t => t.description);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string);
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(string);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var valueAsString = value as string;
			if (valueAsString != null)
			{
				return GetValue(valueAsString);
			}

			return base.ConvertFrom(context, culture, value);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == null) { throw new ArgumentNullException("destinationType"); }

			if (value != null)
			{
				if (value is T && destinationType == typeof(string))
				{
					return GetDescription((T)value);
				}
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool IsValid(ITypeDescriptorContext context, object value)
		{
			// IsValid is used to check if value is a valid enum value - not if its possible to convert to the type
			if (value == null) { throw new ArgumentNullException("value"); }

			if (value is T)
			{
				return enumValueToStringMap.ContainsKey((T)value);
			}

			var key = value as string;
			if (key != null)
			{
				return stringToEnumValueMap.ContainsKey(key);
			}

			if (value is int)
			{
				// This will fall back to reflection, however its none-trivial to roll yourself.
				return Enum.IsDefined(typeof(T), value);
			}

			throw new InvalidOperationException("Unknown enum type.");
		}

		private T GetValue(string value)
		{
			T convert;
			if (stringToEnumValueMap.TryGetValue(value, out convert))
				return convert;

			throw new FormatException(String.Format("{0} is not a valid value for {1}.", value, typeof(T).Name));
		}

		private string GetDescription(T value)
		{
			string convert;
			if (enumValueToStringMap.TryGetValue(value, out convert))
				return convert;

			throw new ArgumentException(String.Format("The value '{0}' is not a valid value for the enum '{1}'.", value, typeof(T).Name));
		}
	}
}
