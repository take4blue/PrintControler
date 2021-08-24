using System;

namespace Take4.Rs274ngcParser {
	/// <summary>
	/// 解析した結果を保存する
	/// </summary>
	public class ValuePosition : ICloneable {
		/// <summary>
		/// キー文字
		/// </summary>
		public Char Key { get; private set; }
		/// <summary>
		/// 実際の値
		/// </summary>
		public Double Value;
		/// <summary>
		/// 値が設定されていた位置
		/// </summary>
		public int Position;
		/// <summary>
		/// 小数点桁数(-1の場合は数値がない場合)
		/// </summary>
		public int FP;

		/// <summary>
		/// キー設定型のコンストラクタ
		/// </summary>
		public ValuePosition(Char key) {
			Key = key;
		}

		/// <summary>
		/// 文字化したものを出力する
		/// </summary>
		/// <returns>文字列</returns>
		public override string ToString() {
			if (FP < 0) {
				return new string(Key, 1);
			}
			else {
				return Key + string.Format("{0:F" + FP.ToString() + "}", Value);
			}
		}

		public object Clone() {
			var result = new ValuePosition(this.Key);
			result.Value = this.Value;
			result.Position = this.Position;
			result.FP = this.FP;
			return result;
		}
	}
}
