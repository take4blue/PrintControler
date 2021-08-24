using System.Collections.Generic;

namespace Take4.Rs274ngcParser {
	/// <summary>
	/// Gコードファイルを出力するためのサポートクラス
	/// </summary>
	static public class OutputGCode {
		/// <summary>
		/// ValuePositionのデータ群をGCodeファイルフォーマットに合わせた文字列化をする
		/// valuesの出力順序は、foreachでの取り出し順序
		/// </summary>
		/// <param name="values">Gコードパラメータ</param>
		/// <param name="comment">コメント(先頭に;が入っていない場合は、;を付けて出力する)</param>
		/// <returns>文字列化した1行のデータ</returns>
		static public string ToString(IEnumerable<ValuePosition> values, string comment = null) {
			string ret = "";

			foreach (var val in values) {
				if (ret.Length > 0) {
					ret += " ";
				}
				ret += val.ToString();
			}

			if (!string.IsNullOrEmpty(comment)) {
				if (!comment.StartsWith(";")) {
					ret += ";";
				}
				ret += comment;
			}

			return ret;
		}

		/// <summary>
		/// 解析結果の内容の順番を保ったまま出力する
		/// ただし、フィールドセパレータ等、元のデータからは正規化された形で出力される
		/// </summary>
		/// <param name="value">解析結果</param>
		/// <param name="addComment">コメントを追加するかどうか</param>
		/// <returns>文字列化した1行のデータ</returns>
		static public string ToString(LineCommand value, bool addComment = true) {
			var cloneValue = value.CloneValues();
			cloneValue.Sort((x, y) => x.Position - y.Position);
			return ToString(cloneValue, addComment ? value.Comment : null);
		}
	}
}
