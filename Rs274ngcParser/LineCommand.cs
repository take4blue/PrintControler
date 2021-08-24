using System;
using System.Collections.Generic;
using System.Linq;

namespace Take4.Rs274ngcParser {
	/// <summary>
	/// 1行を解析した結果を保持する
	/// </summary>
	public class LineCommand {
		/// <summary>
		/// Keyを第1キーにしたSortedSetのソートキー
		/// </summary>
		class CompareKey : IComparer<ValuePosition> {
			public int Compare(ValuePosition x, ValuePosition y) {
				return x.Key - y.Key;
			}
		}

		SortedSet<ValuePosition> keyValues_ = new SortedSet<ValuePosition>(new CompareKey());

		/// <summary>
		/// 実データと、設定されていた場所の情報を取り出す
		/// </summary>
		/// <param name="key">取り出すデータのキー文字</param>
		/// <param name="value">設定されていた値</param>
		/// <param name="position">設定されていた位置(0オリジン)</param>
		/// <returns>キーに対する情報が設定されていたかどうか、truの場合設定されていた</returns>
		public bool TryGetValue(Char key, out Double value, out int position) {
			ValuePosition result;
			var serachKey = new ValuePosition(key);
			if (keyValues_.TryGetValue(serachKey, out result)) {
				value = result.Value;
				position = result.Position;
				return true;
			}
			else {
				value = 0.0;
				position = -1;
				return false;
			}
		}

		/// <summary>
		/// データ数の取得
		/// </summary>
		public int Count { get => keyValues_.Count; }

		/// <summary>
		/// 指定した位置にあるキーを取り出す
		/// </summary>
		/// <param name="pos">位置</param>
		/// <returns>キー、' 'の場合はキーがない</returns>
		public Char GetKey(int pos) {
			if (pos >= 0 && pos < keyValues_.Count) {
				foreach (var val in keyValues_) {
					if (val.Position == pos) {
						return val.Key;
					}
				}
			}
			return ' ';
		}

		/// <summary>
		/// 数値部の小数点桁数に関する情報
		/// </summary>
		/// <param name="key">取り出すデータのキー文字</param>
		/// <returns>小数点桁数</returns>
		public int GetFP(Char key) {
			ValuePosition result;
			var searchKey = new ValuePosition(key);
			if (keyValues_.TryGetValue(searchKey, out result)) {
				return result.FP;
			}
			else {
				return 0;
			}
		}

		/// <summary>
		/// データの修正を行う
		/// </summary>
		/// <example>
		/// 例えば、キー X の値に5を足したい場合
		/// <code>
		/// LineCommand.Modify('X', (x) => x.Value += 5);
		/// </code>
		/// </example>
		/// <param name="key">修正を行うキー文字</param>
		/// <param name="action">修正関数(ValuePositionを引数で受け取る)</param>
		/// <returns>キー文字がない場合false</returns>
		public bool Modify(Char key, Action<ValuePosition> action) {
			ValuePosition result;
			var serachKey = new ValuePosition(key);
			if (keyValues_.TryGetValue(serachKey, out result)) {
				action(result);
				return true;
			}
			else {
				return false;
			}
		}

		/// <summary>
		/// データを最後に追加
		/// </summary>
		/// <param name="key">追加するキー文字</param>
		/// <param name="value">値</param>
		/// <param name="fp">小数点桁数</param>
		/// <returns></returns>
		public bool Add(Char key, Double value, int fp = 0) {
			var valuePair = new ValuePosition(key) { Value = value, Position = keyValues_.Count, FP = fp };
			return keyValues_.Add(valuePair);
		}

		/// <summary>
		/// キーの文字が読み込まれているか
		/// </summary>
		/// <param name="key">チェックするキー文字</param>
		/// <returns>キー文字が含まれている</returns>
		public bool Has(Char key) {
			ValuePosition result;
			var searchKey = new ValuePosition(key);
			return keyValues_.TryGetValue(searchKey, out result);
		}

		string Comment_ = null;
		/// <summary>
		/// コメント情報(含まれていなければnull)
		/// </summary>
		public string Comment { get => Comment_; }

		string Original_ = null;
		/// <summary>
		/// オリジナルの1行の情報
		/// </summary>
		public string Original { get => Original_; }

		/// <summary>
		/// 解析結果の内容の一括取り出し
		/// </summary>
		/// <returns>クローンされたデータ</returns>
		public List<ValuePosition> CloneValues() {
			var ret = new List<ValuePosition>();
			ret.AddRange(keyValues_.Select(i => (ValuePosition)i.Clone()));
			return ret;
		}

		/// <summary>
		/// 文字列の中から英文字(1文字)をコマンドとして、その後に続く数値と.と-を値として解析して返す
		/// </summary>
		/// <param name="line">ファイル中の1行の情報</param>
		/// <param name="command">コマンド文字を小文字にしたもの</param>
		/// <param name="value">数値らしき値</param>
		/// <param name="FP">小数点以下の桁数</param>
		/// <returns>commandとvalueを除いた値</returns>
		private string ParseCommand(string line, out char command, out Double value, out int FP) {
			var work = line.Trim();
			command = ' ';
			value = 0.0;
			FP = 0;
			bool hasDigit = false;
			string digitString = "";
			if (work.Length > 0) {
				command = work[0];
				int next = 1;
				for (bool dotStart = false; next < work.Length && (Char.IsDigit(work[next]) || work[next] == '-' || work[next] == '.'); next++) {
					hasDigit = true;
					digitString += work[next];
					if (work[next] == '.') {
						dotStart = true;
					}
					else if (dotStart) {
						++FP;
					}
				}
				Double result = 0.0;
				if (Double.TryParse(digitString, out result)) {
					value = result;
				}
				if (!hasDigit) {
					FP = -1;
				}

				work = work.Substring(next, work.Length - next);
			}
			return work;
		}

		/// <summary>
		/// 1行の解析
		/// </summary>
		/// <param name="line">解析をする行のデータ</param>
		/// <returns>異常な行の場合falseとなり、設定されている内容についての信頼性はない</returns>
		/// <remarks>同じコマンドが2つあった場合、異常とする</remarks>
		public bool Parse(string line) {
			keyValues_.Clear();
			Original_ = line;
			if (line.StartsWith(";")) {
				Comment_ = line.TrimEnd('\r', '\n');
			}
			else {
				var work = line.TrimEnd('\r', '\n');
				var commentPos = work.IndexOf(';');
				if (commentPos >= 0) {
					Comment_ = work.Remove(0, commentPos);
					work = work.Remove(commentPos);
				}
				for (int i = 0; work.Length > 0; ++i) {
					char command;
					Double value;
					int fp;
					work = ParseCommand(work, out command, out value, out fp);
					if (command == ' ') {
						// データが読み込まれていない場合なので
						break;
					}
					var valuePair = new ValuePosition(command) { Value = value, Position = i, FP = fp };
					if (!keyValues_.Add(valuePair)) {
						return false;
					}
				}
			}
			return true;
		}
	}
}
