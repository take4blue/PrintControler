using System.Windows.Controls;
using Prism.Regions;
using System.Windows;

namespace PrintControler.Views {
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	[ViewSortHint("0")]		// Tabの1番目に表示させるための設定
	public partial class Print : UserControl {
		public Print() {
			InitializeComponent();
		}
		private dynamic VM {
			get { return this.DataContext; }
		}
		private void dropEvent(object target, DragEventArgs action) {
			if (action.Data.GetDataPresent(DataFormats.FileDrop)) {
				var files = action.Data.GetData(DataFormats.FileDrop) as string[];
				if (VM.DropCommand.CanExecute(files)) {
					VM.DropCommand.Execute(files);
				}
			}
			action.Handled = true;
		}
		private void dragEvent(object target, DragEventArgs action) {
			action.Effects = DragDropEffects.None;
			if (action.Data.GetDataPresent(DataFormats.FileDrop)) {
				var files = action.Data.GetData(DataFormats.FileDrop) as string[];
				if (VM.DropCommand.CanExecute(files)) {
					action.Effects = DragDropEffects.Copy;
				}
			}
			action.Handled = true;
		}
	}
}
