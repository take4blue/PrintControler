using PrintControler.Controler;
using System.Windows;
using System.Windows.Input;

namespace PrintControler.Views {
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	/// <remarks>
	/// 閉じる処理はApplicationCommands.Closeのcanメソッドは使わない。
	/// CloseのcanメソッドはそれにバインドされたUIが押せなくなる状態にさせるだけ。
	/// closeではあまりそのようなことはさせたくないため、次のようにしている。
	/// ・execメソッド内で、登録されているCompositeCommandのcanメソッドの判定を行う
	/// ・上記でtrueの場合、初めてCompositeCommandのexecメソッドを実行する
	/// </remarks>
	public partial class Shell : Window {
		IApplicationCommands cmds_;

		public Shell(IApplicationCommands cmd) {
			cmds_ = cmd;
			InitializeComponent();
			Closing += WindowClosing;
			CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, CloseWindow));
			ApplicationCommands.Close.InputGestures.Add(new KeyGesture(Key.X, ModifierKeys.Control, "Ctrl+x"));
		}

		#region 閉じる処理
		private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e) {
			//新規作成で行う処理が実装されます。
			if (cmds_.CloseCommand.CanExecute(null)) {
				cmds_.CloseCommand.Execute(null);
				e.Cancel = false;
			}
			else {
				e.Cancel = true;
			}
		}
		private void CloseWindow(object sender, ExecutedRoutedEventArgs e) {
			if (cmds_.CloseCommand.CanExecute(null)) {
				cmds_.CloseCommand.Execute(null);
				Close();
			}
		}
		#endregion
	}
}
