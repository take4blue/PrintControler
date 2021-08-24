using System;
using System.Globalization;
using System.Windows.Data;

namespace Take4.Common {
	/// <summary>
	/// MaxLengthをWidthに変換するもの
	/// </summary>
	[ValueConversion(typeof(int), typeof(int))]
	public class LengthToWidthConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			var length = value as int?;
			if (!length.HasValue) { return null; }
			return (length.Value + 1) * 7;
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			var length = value as int?;
			if (!length.HasValue) { return null; }
			return (length.Value / 7) - 1;
		}
	}

	/// <summary>
	/// MM/S→MM/M
	/// </summary>
	[ValueConversion(typeof(int), typeof(int))]
	public class MMStoMMMConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			var length = value as int?;
			if (!length.HasValue) { return null; }
			return length.Value * 60;
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			var length = value as int?;
			if (!length.HasValue) { return null; }
			return length.Value / 60;
		}
	}

	/// <summary>
	/// MM/M→MM/S
	/// </summary>
	[ValueConversion(typeof(int), typeof(int))]
	public class MMMtoMMSConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			var length = value as int?;
			if (!length.HasValue) { return null; }
			return length.Value / 60;
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value.GetType() == typeof(int)) {
				return (int)value * 60;
			}
			else if (value.GetType() == typeof(string)) {
				int length;
				if (int.TryParse((string)value, out length)) {
					return length * 60;
				}
			}
			return null;
		}
	}

	/// <summary>
	/// 列挙型とbool型の変換器
	/// </summary>
	[ValueConversion(typeof(Enum), typeof(bool))]
	public class EnumToBoolConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {

			var ParameterString = parameter as string;
			if (ParameterString == null) {
				return System.Windows.DependencyProperty.UnsetValue;
			}

			if (Enum.IsDefined(value.GetType(), value) == false) {
				return System.Windows.DependencyProperty.UnsetValue;
			}

			object paramvalue = Enum.Parse(value.GetType(), ParameterString);

			return (int)paramvalue == (int)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if ((bool) value) {
				return Enum.Parse(targetType, parameter.ToString());
			}
			else {
				return System.Windows.DependencyProperty.UnsetValue;
			}
		}
	}

}
