using System;
using Take4.Common;

namespace PrintControler.MFilamentLoad.ViewModels {
	/// <summary>
	/// フィラメントの挿入・抜去とノズル清掃に関するViewModel
	/// </summary>
	internal class FilamentLoadViewModel : ViewBase<ControlData>, System.IDisposable, Prism.IActiveAware {
		/// <summary>
		/// タブに表示される文字(バインド)
		/// </summary>
		public string HeaderText { get; private set; }

		Controler.IAdventurer3Controler ctrl_;

		/// <summary>
		/// アプリケーションコマンド対応
		/// </summary>
		private Controler.IApplicationCommands cmds_;

		#region コマンド類
		private Prism.Commands.DelegateCommand<Object> CloseCmd { get; set; }
		public Prism.Commands.DelegateCommand<string> ExecuteCommand { get; set; }
		#endregion

		/// <summary>
		/// 何らかのコマンドが実行された場合trueにする。
		/// これは、プログラムが終了した時、このタブから抜けた時に、機器の状態を印刷可能状態に戻すためのフラグ
		/// </summary>
		bool isCommandExecute_ = false;

		public FilamentLoadViewModel(Controler.IAdventurer3Controler model, Controler.IApplicationCommands cmds) : base() {
			parameter_ = new ControlData(model);
			ctrl_ = model;
			cmds_ = cmds;
			HeaderText = Properties.Resources.TabTitle;

			// パラメータの読み込み
			ParameterFileName = ctrl_.BaseFolderName + @"\FilamentLoad.json";
			LoadParameter();

			// 閉じる処理用コマンドの追加
			CloseCmd = new Prism.Commands.DelegateCommand<Object>(x => {
				SaveParameter();
				ExecClose();
			});
			ExecuteCommand = new Prism.Commands.DelegateCommand<string>(
				x => ActionCommand(x),
				x => CanExecuteCommand).ObservesProperty(() => CanExecuteCommand);

			// イベント処理関連の登録処理
			cmds_.CloseCommand.RegisterCommand(CloseCmd);
			ctrl_.PropertyChanged += ChangedProperty; // 機器側からの更新通知の受信設定
		}

		/// <summary> 一応本タブアイテムが除去された場合の各種開放処理を入れておく </summary>
		public void Dispose() {
			ctrl_.PropertyChanged -= ChangedProperty;
			cmds_.CloseCommand.UnregisterCommand(CloseCmd);
		}

		/// <summary>
		/// このパラメータ画面から抜ける際の共通処理
		/// 温度設定を0にしておく、またこのパラメータ画面で移動処理を行っていたら、緊急停止コマンドを送付しXYZ座標を確定させておく
		/// </summary>
		private void ExecClose() {
			if (ctrl_.IsConnected && isCommandExecute_) {
				ctrl_.EmergencyStop();
				ctrl_.TargetTempNozel = 0;
			}
			parameter_.StopCommand();
			isCommandExecute_ = false;
		}

		private bool CanExecuteCommand {
			get => ctrl_.CanJobStart && ctrl_.IsConnected;
		}
		private void ActionCommand(string value) {
			ControlData.CommandType action = (ControlData.CommandType)Enum.Parse(typeof(ControlData.CommandType), value);
			parameter_.ExecuteCommand(action, cleanType_);
			RaisePropertyChanged(nameof(NowCommandStatus));
			isCommandExecute_ = true;
			if (action == ControlData.CommandType.CleanNozzle) {
				// ノズル清掃の場合、次の実行項目に移す
				switch(CleanType) {
				case ControlData.NozzleCleanType.CleanupPreProcess:
					CleanType = ControlData.NozzleCleanType.NozzleLowTemp;
					break;
				case ControlData.NozzleCleanType.FilamentCutPreProcess:
					CleanType = ControlData.NozzleCleanType.TubeInsertPreProcess;
					break;
				case ControlData.NozzleCleanType.TubeInsertPreProcess:
					CleanType = ControlData.NozzleCleanType.FilamentInsert;
					break;
				}
			}
		}

		public int SpeedEHigh {
			get => parameter_.SpeedEHigh;
			set => SetProperty(value);
		}

		public int SpeedELow {
			get => parameter_.SpeedELow;
			set => SetProperty(value);
		}

		public int SpeedXY {
			get => parameter_.SpeedXY;
			set => SetProperty(value);
		}

		public int SpeedZ {
			get => parameter_.SpeedZ;
			set => SetProperty(value);
		}

		public int TargetLowTempNozel {
			get => parameter_.TargetLowTempNozel;
			set => SetProperty(value);
		}

		public int TargetHighTempNozel {
			get => parameter_.TargetHighTempNozel;
			set => SetProperty(value);
		}

		public int CurrentTempNozel {
			get => ctrl_.CurrentTempNozel;
		}

		private ControlData.NozzleCleanType cleanType_ = ControlData.NozzleCleanType.CleanupPreProcess;
		public ControlData.NozzleCleanType CleanType {
			get => cleanType_;
			set => SetProperty(ref cleanType_, value);
		}

		public string NowCommandStatus {
			get => parameter_.NowCommandStatus;
		}

		/// <summary>
		/// パラメータ読み込み後の更新通知
		/// </summary>
		protected override void RaisePropertyAfterLoadParameter() {
			base.RaisePropertyAfterLoadParameter();
			RaisePropertyChanged(nameof(SpeedEHigh));
			RaisePropertyChanged(nameof(SpeedELow));
			RaisePropertyChanged(nameof(SpeedXY));
			RaisePropertyChanged(nameof(SpeedZ));
			RaisePropertyChanged(nameof(TargetLowTempNozel));
			RaisePropertyChanged(nameof(TargetHighTempNozel));
		}

		/// <summary>
		/// modelのプロパティ変更対応のイベント処理
		/// </summary>
		/// <param name="sender">オブジェクト名</param>
		/// <param name="eventArgs">イベント情報</param>
		private void ChangedProperty(object sender, System.ComponentModel.PropertyChangedEventArgs eventArgs) {
			switch (eventArgs.PropertyName) {
			case Controler.Adventurer3Property.status:
				RaisePropertyChanged(nameof(CurrentTempNozel));
				RaisePropertyChanged(nameof(NowCommandStatus));
				RaisePropertyChanged(nameof(CanExecuteCommand));
				break;
			case Controler.Adventurer3Property.position:
				break;
			case Controler.Adventurer3Property.connect:
				RaisePropertyChanged(nameof(CanExecuteCommand));
				RaisePropertyChanged(nameof(CurrentTempNozel));
				RaisePropertyChanged(nameof(NowCommandStatus));
				CleanType = ControlData.NozzleCleanType.CleanupPreProcess;
				isCommandExecute_ = false;
				break;
			}
		}

		public event EventHandler IsActiveChanged;
		private bool isActive_ = false;
		public bool IsActive {
			get => isActive_;
			set {
				SetProperty(ref this.isActive_, value);
				if (isActive_) {
					CleanType = ControlData.NozzleCleanType.CleanupPreProcess;
				}
				else {
					ExecClose();
				}
				RaisePropertyChanged(nameof(NowCommandStatus));
			}
		}
	}
}
