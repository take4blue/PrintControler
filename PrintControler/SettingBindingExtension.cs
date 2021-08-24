using System.Windows.Data;

namespace PrintControler {
	/// <summary>
	/// http://www.thomaslevesque.com/2008/11/18/wpf-binding-to-application-settings-using-a-markup-extension/
	/// 上記を参考にした設定のバインダー
	/// </summary>
	public class SettingBindingExtension : Binding {
		public SettingBindingExtension() {
			Initialize();
		}

		public SettingBindingExtension(string path)
			: base(path) {
			Initialize();
		}

		private void Initialize() {
			this.Source = PrintControler.Properties.Settings.Default;
			this.Mode = BindingMode.TwoWay;
		}
	}
}
