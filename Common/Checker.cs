using System;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.IO;

namespace Take4.Common {
	/// <summary>
	/// 実数の範囲判断付きの妥当性チェッククラス
	/// </summary>
	public class DoubleFPAttribute : ValidationAttribute {
		public int decimalPoint { set; get; }
		public double min { set; get; }
		public double max { set; get; }

		public DoubleFPAttribute() {
			ErrorMessage = Properties.Resources.MsgOutOfRange;
		}

		protected override ValidationResult IsValid(object value, ValidationContext validationContext) {
			double work = min;
			try {
				if (value.GetType() == typeof(double)) {
					work = Math.Round((double)value, decimalPoint, MidpointRounding.AwayFromZero);
				}
				else if (value.GetType() == typeof(string)) {
					if (((string)value).Length > 0) {
						work = Math.Round(Double.Parse((String)value), decimalPoint, MidpointRounding.AwayFromZero);
					}
				}
			}
			catch (Exception e) {
				return new ValidationResult(e.Message);
			}
			if (work < min || work > max) {
				return new ValidationResult(String.Format(ErrorMessageString, value.ToString(), min, max));
			}
			return ValidationResult.Success;
		}
	}

	public static class Checker {
		/// <summary>
		/// 読み込み可能な状態で、かつ読み込み可能なファイルか
		/// </summary>
		/// <param name="action">ドラッグイベント</param>
		/// <returns>Drag or Dropされたファイル名</returns>
		public static string CanReadDcodeFile(string[] files, bool canJobStart) {
			// ファイルに対しての処理を実施する
			if (canJobStart && files.Length == 1) {
				switch (Path.GetExtension(files[0])) {
				case ".g":
				case ".gx":
					return files[0];
				}
			}
			return null;
		}

		/// <summary>intの範囲チェック</summary>
		public static void RangeCheck<T>(T value, T min, T max, ref System.Collections.Generic.Dictionary<string, string> msg, string name) where T : IComparable {
			if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0) {
				msg[name] = string.Format(Properties.Resources.MsgOutOfRange, value, min, max);
			}
		}
	}
}
