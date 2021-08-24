using Prism.Commands;

namespace PrintControler.Controler {
	/// <summary>
	/// シェルから呼び出される共通のコマンドインターフェース
	/// </summary>
	public interface IApplicationCommands {
		/// <summary>
		/// 閉じるコマンド用
		/// DelgateCommand<Object>を登録可能としている。
		/// Execute, CanExecuteへのパラメータはnullで渡されるので判断しないこと
		/// canメソッドでは、閉じる際の保存ダイアログの表示などをさせる。
		/// 問題なければtrueを返す
		/// </summary>
		CompositeCommand CloseCommand { get; }
		/// <summary>
		/// 接続解除コマンド用
		/// DelgateCommand<Object>を登録可能としている。
		/// Execute, CanExecuteへのパラメータはnullで渡されるので判断しないこと
		/// canメソッドでは、接続解除の前に保存ダイアログの表示などをさせる。
		/// 問題なければtrueを返す
		/// </summary>
		CompositeCommand DisConnectCommand { get; }
	}
}
