using PrintControler.Views;
using Prism.Ioc;
using Prism.Logging;
using Prism.Modularity;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PrintControler {
	/// <summary>
	/// App.xaml の相互作用ロジック
	/// </summary>
	public partial class App {
		protected override Window CreateShell() {
			return Container.Resolve<Shell>();
		}

		protected override void RegisterTypes(IContainerRegistry containerRegistry) {
			// Adventurer3制御用コントローラーモデルを登録
			IControler a3;
			if (PrintControler.Properties.Settings.Default.isDebugMode) {
				var logger = new DebugLogger();
				containerRegistry.RegisterInstance(typeof(ILoggerFacade), logger);
				a3 = new Adventurer3TestStub(logger);
			}
			else {
				a3 = new Adventurer3Controler();
			}
			a3.BaseFolderName = ParameterFileName();
			containerRegistry.RegisterInstance(a3);
			containerRegistry.RegisterInstance<Controler.IAdventurer3Controler>(a3);	// こちらはモジュール側で利用可能なコントローラーモデル
			containerRegistry.RegisterSingleton<Controler.IApplicationCommands, ShellCommand>();
		}
		
		/// <summary>モジュールの登録</summary>
		protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog) {
			moduleCatalog.AddModule<PrintControler.MModifyFile.MModifyFileModule>(InitializationMode.WhenAvailable);
			moduleCatalog.AddModule<PrintControler.MControl.MControlModule>(InitializationMode.WhenAvailable);
			moduleCatalog.AddModule<PrintControler.MFilamentLoad.MFilamentLoadModule>(InitializationMode.WhenAvailable);
			moduleCatalog.AddModule<PrintControler.MModifySlic3r.MModifySlic3rModule>(InitializationMode.WhenAvailable);
		}

		/// <summary>
		/// モジュールの読み込み位置をModulesにする。
		/// </summary>
		protected override IModuleCatalog CreateModuleCatalog() {
			return new DirectoryModuleCatalog() { ModulePath = @".\Modules" };
		}

		/// <summary>
		/// パラメータファイルの保存先名
		/// %APPDATA%\take4blue\%APPNAME%\parameter.json
		/// </summary>
		/// <returns>パラメータファイルの保存先名</returns>
		static string ParameterFileName() {
			var company = Attribute.GetCustomAttribute(System.Reflection.Assembly.GetExecutingAssembly(), typeof(System.Reflection.AssemblyCompanyAttribute)) as System.Reflection.AssemblyCompanyAttribute;
			var folder = System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\" + company.Company + @"\" + Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
			if (!Directory.Exists(folder)) {
				Directory.CreateDirectory(folder);
			}
			return folder;
		}

		private void Application_Exit(object sender, ExitEventArgs e) {
			PrintControler.Properties.Settings.Default.Save();
		}
	}
}
