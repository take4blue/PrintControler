using System.Windows;
using System.Windows.Controls;
using Prism.Regions;

namespace PrintControler.MModifySlic3r.Views {
	/// <summary>
	/// Interaction logic for ViewA.xaml
	/// </summary>
	[ViewSortHint("30")]
	public partial class Parameter : UserControl
    {
        public Parameter()
        {
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
