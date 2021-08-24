using Prism.Events;

namespace PrintControler.Controler {
	/// <summary>
	/// 印刷開始を行ってもらうためのペイロード
	/// </summary>
	public class PrintData {
		/// <summary>
		/// 処理をしてもらいたいモジュール
		/// </summary>
		public enum TargetType {
			/// <summary>
			/// 直接印刷に送る
			/// </summary>
			PrintControler,
			/// <summary>
			/// Adventurer3用にファイルを更新する処理に送る
			/// </summary>
			ModifyToAdventure3,
		}

		public TargetType Type { get; set; } = TargetType.PrintControler;
		/// <summary>
		/// 機器側に表示させたいオリジナルのファイル名
		/// nullの場合、realFileNameを使う
		/// </summary>
		public string OrignalFileName { get; set; }
		/// <summary>
		/// 実体としてのファイル名
		/// オリジナルファイルを更新した作業用ファイルなど、実際に機器側に送信されるファイルを設定する
		/// </summary>
		public string RealFileName { get; set; }
	}

	/// <summary>
	/// イベントアグリゲーター用クラス
	/// IAdventurer3Controler.canJobStartがfalseの場合に発行しても、受け取り側は何もしない可能性がある
	/// </summary>
	public class PrintEvent : PubSubEvent<PrintData> {
	}
}