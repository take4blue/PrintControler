using PrintControler.Controler;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Take4.Common;

namespace PrintControler.ViewModels {
	/// <summary>
	/// 印刷タブ用のViewModel
	/// </summary>
	internal class PrintViewModel : BindableBase, IDisposable {
		private IControler model_;
		private IApplicationCommands cmds_;
		private IEventAggregator ea_;
		private IRegionManager rm_;

		public string HeaderText { get; private set; }

		#region コマンド類
		public DelegateCommand StopAction { get; private set; }
		public DelegateCommand<string[]> DropCommand { get; private set; }
		public DelegateCommand<Object> CloseCmd { get; private set; }
		public DelegateCommand<Object> DisConnectCmd { get; private set; }
		public DelegateCommand PushLed { get; private set; }
		public DelegateCommand PushMovie { get; private set; }
		#endregion

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="model">コントローラーモデル</param>
		/// <param name="cmds">アプリケーション定義のコマンド</param>
		public PrintViewModel(IControler model, IApplicationCommands cmds, IEventAggregator ea, IRegionManager rm) {
			HeaderText = Properties.Resources.TabPrint;
			model_ = model;
			cmds_ = cmds;
			ea_ = ea;
			rm_ = rm;

			StopAction = new DelegateCommand(() => StopEvent(), () => CanStop).ObservesProperty(() => CanStop);
			PushLed = new DelegateCommand(() => model_.Led(btnLed_), () => IsConnected).ObservesProperty(() => IsConnected);
			PushMovie = new DelegateCommand(() => {
				if (IsConnected) {
					model_.StreamAction(btnMovie_);
				}
			}
			);
			DropCommand = new DelegateCommand<string[]>(
				x => {
					var file = Checker.CanReadDcodeFile(x, IsConnected && model.Status == MachineStatus.Ready);
					if (file != null) {
						PrintStart(file, file);
					}
				},
				x => {
					var file = Checker.CanReadDcodeFile(x, IsConnected && model.Status == MachineStatus.Ready);
					return file != null ? true : false;
				});
			// 閉じるは、閉じることが可能かどうかのみ定義
			CloseCmd = new DelegateCommand<object>(x => { }, x => CanClose(x));

			ea_.GetEvent<PrintEvent>().Subscribe(PrintExec, ThreadOption.PublisherThread, false,
				x => x.Type == PrintData.TargetType.PrintControler);	// 印刷イベントを受信可能にしておく

			cmds_.CloseCommand.RegisterCommand(CloseCmd);
			cmds_.DisConnectCommand.RegisterCommand(CloseCmd);	// 接続解除コマンドはクローズコマンドと同じとしておく
			model_.PropertyChanged += ChangedProperty;
		}

		public void Dispose() {
			model_.PropertyChanged -= ChangedProperty;
			cmds_.CloseCommand.UnregisterCommand(CloseCmd);
			cmds_.DisConnectCommand.UnregisterCommand(CloseCmd);
			ea_.GetEvent<PrintEvent>().Unsubscribe(PrintExec);
		}

		private bool CanClose(Object target) {
			if (model_.IsConnected && model_.Status == MachineStatus.Transfer) {
				// モデル転送途中は終了できない
				return false;
			}
			return true;
		}
		private bool IsConnected { get => model_.IsConnected; }

		private bool btnLed_ = true;
		public bool BtnLed {
			get => btnLed_;
			set => SetProperty(ref btnLed_, value);
		}
		private bool btnMovie_ = true;
		public bool BtnMovie {
			get => btnMovie_;
			set => SetProperty(ref btnMovie_, value);
		}

		#region 印刷関連
		/// <summary>
		/// ストップ可能かどうか
		/// </summary>
		private bool CanStop {
			// ビルド途中や、データを転送途中の場合STOP可能にする
			get => model_.IsConnected &&
				(model_.Status == MachineStatus.Building || model_.Status == MachineStatus.Transfer);
		}
		private void StopEvent() {
			if (CanStop) {
				model_.StopJob();
				ProgressValue = 0;
			}
		}

		/// <summary>
		/// 出力するファイルのオリジナルのファイル名
		/// </summary>
		private string originalFileName_;
		/// <summary>
		/// 出力する実ファイル名
		/// </summary>
		private string realFileName_;

		/// <summary>
		/// 印刷開始
		/// 非同期で、転送と印刷開始コマンドの出力まで行わせる。
		/// </summary>
		private async void PrintStart(string originalFileName, string realFileName) {
			realFileName_ = realFileName;
			originalFileName_ = originalFileName;
			ProgressValue = 0;
			if (File.Exists(realFileName_)) {
				bool result = await model_.ExecuteGFile(realFileName_, Path.GetFileNameWithoutExtension(originalFileName_));
				if (!result) {
					// 実行失敗(途中で停止を押した可能性がある)
					originalFileName_ = null;
					RaisePropertyChanged(nameof(DropAreaLabel));
				}
				RaisePropertyChanged(nameof(CanStop));
				RaisePropertyChanged(nameof(IsConnected));
			}
		}

		/// <summary>
		/// イベントアグリゲーターで来た印刷イベントを処理
		/// </summary>
		/// <param name="message">印刷のための情報</param>
		private void PrintExec(PrintData message) {
			rm_.RequestNavigate(Adventurer3Property.tabRegionName, nameof(Views.Print));		// 自分自身にページ遷移
			PrintStart(message.OrignalFileName ?? message.RealFileName, message.RealFileName);
		}

		public string DropAreaLabel {
			get {
				if (model_.IsConnected) {
					switch (model_.Status) {
					case MachineStatus.Ready:
						return Properties.Resources.MsgDropArea;
					case MachineStatus.Building:
					case MachineStatus.Transfer:
						return Properties.Resources.MsgDropArea + "\r\n" + originalFileName_ ?? "";
					}
				}
				return "";
			}
		}
		#endregion

		#region プログレスバーに対しての処理
		public long ProgressMax {
			get {
				switch(model_.Status) {
				case MachineStatus.Transfer:
					return model_.TransferByte;
				default:
					return 100;
				}
			}
		}
		private long progressValue_ = 0;
		public long ProgressValue {
			get => progressValue_;
			set => SetProperty(ref progressValue_ ,value);
		}
		#endregion

		public BitmapImage ImageSource {
			get => model_.Image;
		}

		/// <summary>
		/// modelのプロパティ変更対応のイベント処理
		/// </summary>
		/// <param name="sender">オブジェクト名</param>
		/// <param name="eventArgs">イベント情報</param>
		private void ChangedProperty(object sender, PropertyChangedEventArgs eventArgs) {
			switch (eventArgs.PropertyName) {
			case Adventurer3Property.status:           // 機器のステータス更新がされたので、表示用のステータスを更新対象とする
				switch (model_.Status) {
				case MachineStatus.Transfer:
					ProgressValue = model_.RemainTransferByte > 0 ? model_.TransferByte - model_.RemainTransferByte : 0;
					break;
				case MachineStatus.Building:
					ProgressValue = model_.SdProgress;
					break;
				default:
					ProgressValue = 0;
					break;
				}
				RaisePropertyChanged(nameof(ProgressMax));
				RaisePropertyChanged(nameof(CanStop));
				RaisePropertyChanged(nameof(DropAreaLabel));
				break;
			case Adventurer3Property.connect:
				RaisePropertyChanged(nameof(IsConnected));
				if (!IsConnected) {
					originalFileName_ = null;
				}
				else if (BtnMovie) {
					model_.StreamAction(BtnMovie);
				}
				break;
			case IControlerProperty.image:
				RaisePropertyChanged(nameof(ImageSource));
				break;
			}
		}
	}
}
