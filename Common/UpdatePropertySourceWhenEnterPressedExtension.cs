using System;

namespace Take4.Common {
	public class UpdatePropertySourceWhenEnterPressedExtension : System.Windows.Markup.MarkupExtension {
		public override object ProvideValue(IServiceProvider serviceProvider) {
			return new Prism.Commands.DelegateCommand<System.Windows.Controls.TextBox>(textbox => textbox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty).UpdateSource());
		}
	}
}
