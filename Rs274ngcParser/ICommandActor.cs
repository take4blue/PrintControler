namespace Take4.Rs274ngcParser {
	/// <summary>
	/// Gコードの解析に関してのアクションクラス用のインターフェース
	/// </summary>
	public interface ICommandActor {
		/// <summary>
		/// 事前処理用
		/// </summary>
		void PreAction();

		/// <summary>
		/// 事後処理用
		/// </summary>
		void PostAction();

		/// <summary>
		/// 1行情報の対処
		/// </summary>
		/// <param name="line">行の情報</param>
		/// <returns>falseの場合解析を終了させる</returns>
		bool ActionLine(LineCommand line);
	}
}
