using System;

namespace Take4.Rs274ngcParser {

	/// <summary>
	/// XYZEFの値を計算・保持するクラス
	/// </summary>
	/// <remarks>
	/// 先頭がG1の場合のみ、値を更新する<br/>
	/// 座標系はすべてmmで保持している
	/// </remarks>
	public class ValueXYZEF : ICommandActor {
		public Double X { get; set; }
		public Double Y { get; set; }
		public Double Z { get; set; }
		public Double E { get; set; }
		public Double F { get; set; }

		/// <summary>
		/// 絶対値モード
		/// </summary>
		bool IsAbsolute_ = true;
		/// <summary>
		/// 単位系の指定
		/// </summary>
		bool IsMM_ = true;

		/// <summary>
		/// 絶対値モード
		/// </summary>
		public bool IsAbsolute { get => IsAbsolute_; }

		/// <summary>
		/// 単位がmmモード
		/// </summary>
		public bool IsMM { get => IsMM_; }

		private Double Update(LineCommand line, Char key, Double old) {
			Double value;
			int position;
			if (line.TryGetValue(key, out value, out position)) {
				if (!IsMM_) {
					value *= 2.54;
				}
				if (!IsAbsolute_) {
					value += old;
				}
				return value;
			}
			else {
				return old;
			}
		}

		/// <summary>
		/// 1行処理
		/// </summary>
		/// <param name="line">行の情報</param>
		/// <returns>いつもtrueで返す</returns>
		public bool ActionLine(LineCommand line) {
			Double value;
			int position;
			if (line.TryGetValue('G', out value, out position)) {
				switch (value) {
				case 91:
					IsAbsolute_ = false;
					break;
				case 90:
					IsAbsolute_ = true;
					break;
				case 20:
					IsMM_ = false;
					break;
				case 21:
					IsMM_ = true;
					break;
				case 1:
					X = Update(line, 'X', X);
					Y = Update(line, 'Y', Y);
					Z = Update(line, 'Z', Z);
					E = Update(line, 'E', E);
					F = Update(line, 'F', F);
					break;
				}
			}
			return true;
		}

		public void PostAction() {
			// 何もしない
		}

		public void PreAction() {
			// 初期処理
			IsAbsolute_ = true;
			IsMM_ = true;
			X = Y = Z = E = F = 0.0;
		}
	}
}
