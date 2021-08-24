using Prism.Commands;

namespace PrintControler {
	/// <summary>
	/// Shellで操作する共通コマンド
	/// </summary>
	internal class ShellCommand : Controler.IApplicationCommands {
		private CompositeCommand closeCommand_ = new CompositeCommand();
		public CompositeCommand CloseCommand {
			get => closeCommand_;
		}

		private CompositeCommand disConnectCommand_ = new CompositeCommand();
		public CompositeCommand DisConnectCommand {
			get => disConnectCommand_;
		}
	}
}
