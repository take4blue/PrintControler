using System;
using System.ComponentModel;
using System.IO;
using PrintControler.Controler;
using Prism.Commands;
using Prism.Events;
using Take4.Common;

namespace PrintControler.MModifySlic3r.ViewModels {
	/// <summary>
	/// Sclic3rからMModifyFileへ渡すためのファイルを作る
	/// </summary>
	/// <remarks>パラメータの保存ファイル名はslic3rparam.json</remarks>
	internal class ParameterViewModel : ViewBase<ModifySlic3r>, IDisposable {
		/// <summary>タブに表示される文字(バインド)</summary>
		public string HeaderText { get; private set; }

		/// <summary>Dropエリアに表示される文字列</summary>
		private string addLabel_;

		IApplicationCommands cmds_;
		IEventAggregator ea_;
		IAdventurer3Controler model_;

		private TemporaryFile tempFile_ = new TemporaryFile();

		private DelegateCommand<Object> CloseCmd { get; set; }
		public DelegateCommand<string[]> DropCommand { get; private set; }

		public ParameterViewModel(IAdventurer3Controler model, IApplicationCommands cmds, IEventAggregator ea) {
			model_ = model;
			cmds_ = cmds;
			ea_ = ea;
			HeaderText = Properties.Resources.TabTitle;

			// パラメータ類の処理
			parameter_ = new ModifySlic3r();
			ParameterFileName = model.BaseFolderName + @"\slic3rparam.json";
			LoadParameter();

			// コマンド生成
			CloseCmd = new DelegateCommand<Object>(x => DoClose(x));
			DropCommand = new DelegateCommand<string[]>(
				x => DropAction(x),
				x => {
					var file = Checker.CanReadDcodeFile(x, model_.CanJobStart);
					return file != null ? true : false;
				});

			// イベント処理関連の登録処理
			cmds_.CloseCommand.RegisterCommand(CloseCmd);
			model_.PropertyChanged += ChangedProperty; // 機器側からの更新通知の受信設定
		}

		public void Dispose() {
			model_.PropertyChanged -= ChangedProperty;
			cmds_.CloseCommand.UnregisterCommand(CloseCmd);
			tempFile_.Dispose();
		}

		private void DoClose(Object target) {
			tempFile_.Delete();
			if (!HasErrors) {
				// パラメータの保存処理
				if (!string.IsNullOrEmpty(model_.BaseFolderName)) {
					SaveParameter();
				}
			}
		}

		#region ドロップ処理類
		/// <summary>
		/// ドロップイベントの処理ルーチン
		/// </summary>
		/// <param name="parameter">ドロップされたファイル名</param>
		private void DropAction(string[] parameter) {
			if (!HasErrors) {
				var file = Checker.CanReadDcodeFile(parameter, model_.CanJobStart);
				FileModify(file, file);
			}
		}

		/// <summary>
		/// scli3rからToAdventurer3変換用ファイルに修正し、MModifyFileにファイルを送信する
		/// </summary>
		/// <param name="originalFileName">もともとのファイル名</param>
		/// <param name="realFilename">実際に処理をするファイル名</param>
		private void FileModify(string originalFileName, string realFilename) {
			if (!HasErrors && !string.IsNullOrEmpty(realFilename) && File.Exists(realFilename)) {
				addLabel_ = "";
				tempFile_.Delete();
				bool execPrintJob = false;
				using (var input = File.OpenRead(realFilename)) {
					using (var output = File.Create(tempFile_.FileName)) {
						if (!parameter_.Modify(input, output)) {
							SetProperty(ref addLabel_, string.Format("\r\n" + Properties.Resources.MsgCantConvert, realFilename), nameof(DropAreaLabel));
							tempFile_.Delete();
						}
						else {
							execPrintJob = true;
						}
					}
				}
				if (execPrintJob) {
					// 印刷ジョブにファイルを渡す
					ea_.GetEvent<PrintEvent>().Publish(new PrintData() { OrignalFileName = originalFileName, RealFileName = tempFile_.FileName, Type = PrintData.TargetType.ModifyToAdventure3 });
				}
			}
		}

		/// <summary>
		/// ドロップエリアに表示するラベル名
		/// </summary>
		public string DropAreaLabel {
			get {
				if (model_.CanJobStart) {
					if (HasErrors) {
						return Properties.Resources.MsgParameterError;
					}
					else {
						return Properties.Resources.MsgDropArea + addLabel_ ?? "";
					}
				}
				else {
					return "";
				}
			}
		}
		#endregion

		#region パラメータ処理
		public int SpeedZ {
			get => parameter_.SpeedZ;
			set => SetProperty(value);
		}

		/// <summary>
		/// パラメータ読み込み後の更新通知
		/// </summary>
		protected override void RaisePropertyAfterLoadParameter() {
			base.RaisePropertyAfterLoadParameter();
			RaisePropertyChanged(nameof(SpeedZ));
		}
		#endregion

		/// <summary>
		/// modelのプロパティ変更対応のイベント処理
		/// </summary>
		/// <param name="sender">オブジェクト名</param>
		/// <param name="eventArgs">イベント情報</param>
		private void ChangedProperty(object sender, PropertyChangedEventArgs eventArgs) {
			switch (eventArgs.PropertyName) {
			case Adventurer3Property.status:           // 機器のステータス更新がされたので、表示用のステータスを更新対象とする
			case Adventurer3Property.connect:           // 機器のステータス更新がされたので、表示用のステータスを更新対象とする
				RaisePropertyChanged(nameof(DropAreaLabel));
				break;
			}
		}
	}
}
