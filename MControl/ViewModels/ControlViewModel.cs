using PrintControler.Controler;
using Prism;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;

namespace PrintControler.MControl.ViewModels {
	internal class ControlViewModel : BindableBase, INotifyDataErrorInfo, IDisposable, IActiveAware {
		/// <summary>
		/// タブに表示される文字(バインド)
		/// </summary>
		public string HeaderText { get; private set; }

		/// <summary>
		/// Adventurer3の制御用インターフェース
		/// </summary>
		IAdventurer3Controler ctrl_;

		/// <summary>
		/// 画面中のパラメータ類
		/// </summary>
		ControlData parameter_ = new ControlData();

		/// <summary>
		/// アプリケーションコマンド対応
		/// </summary>
		private IApplicationCommands cmds_;

		/// <summary>
		/// パラメータ画面中で、移動コマンドを使っていたらtrueにしておく
		/// </summary>
		private bool isMoved_ = false;

		/// <summary>
		/// 保存用のパラメータファイル名
		/// </summary>
		private string parameterFileName_;

		#region コマンド類
		private DelegateCommand<Object> CloseCmd { get; set; }
		public DelegateCommand HeatNozel { get; private set; }
		public DelegateCommand HeatBed { get; private set; }
		public DelegateCommand EmrgencyStop { get; private set; }
		public DelegateCommand MoveCommand { get; private set; }
		public DelegateCommand AddRowCommand { get; private set; }
		public DelegateCommand DeleteRowCommand { get; private set; }
		#endregion

		public ControlViewModel(IAdventurer3Controler ctrl, IApplicationCommands cmds)
        {
			HeaderText = PrintControler.MControl.Properties.Resources.TabTitle;
			ctrl_ = ctrl;
			cmds_ = cmds;
			this.ErrorsContainer = new ErrorsContainer<string>(
				x => this.ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(x)));
			ctrl_.PropertyChanged += ChangedProperty; // 機器側からの更新通知の受信設定

			// チェックBOXがONになったら、テキストエリアに設定されている温度を機器に送信する
			// ただしテキストの内容が0の場合、とりあえず温度設定を送るが、チェックボックスもOFFにしておく
			HeatNozel = new DelegateCommand(() => {
				if (!HasErrors) {
					ctrl_.TargetTempNozel = doHeatNozel_ ? TargetTempNozel : 0;
					if (TargetTempNozel == 0) {
						DoHeatNozel = false;
					}
				}
				else {
					DoHeatNozel = false;
				}
			}, () => CanCommandExecute && !HasErrors).ObservesProperty(() => CanCommandExecute);
			HeatBed = new DelegateCommand(() => {
				if (!HasErrors) {
					ctrl_.TargetTempBed = doHeatBed_ ? TargetTempBed : 0;
					if (TargetTempBed == 0) {
						DoHeatBed = false;
					}
				}
				else {
					DoHeatBed = false;
				}
			}, () => CanCommandExecute && !HasErrors).ObservesProperty(() => CanCommandExecute);

			EmrgencyStop = new DelegateCommand(() => { ctrl_.EmergencyStop(); isMoved_ = false; }, () => CanCommandExecute)
				.ObservesProperty(() => CanCommandExecute);
			MoveCommand = new DelegateCommand(() => ExecuteMove(), () => ctrl_.CanJobStart && selectedItem_ != null)
				.ObservesProperty(() => SelectedItem).ObservesProperty(() => CanCommandExecute);	// 選択行の更新と実行可能状況をチェックしておく

			// 閉じる処理用コマンドの追加
			CloseCmd = new DelegateCommand<Object>(x => { SaveCurrentData(); ExecClose(); });
			AddRowCommand = new DelegateCommand(() => {
				MoveData.Add(new MoveElement());
			});
			DeleteRowCommand = new DelegateCommand(() => MoveData.Remove(selectedItem_), () => selectedItem_ != null)
				.ObservesProperty(() => SelectedItem);	// 選択行の更新をチェックしておく
			cmds_.CloseCommand.RegisterCommand(CloseCmd);
			cmds_.DisConnectCommand.RegisterCommand(CloseCmd);

			parameterFileName_ = ctrl_.BaseFolderName + @"\MoveData.json";
			LoadParameter(parameterFileName_);
		}

		#region TAB制御情報の取得(自分がアクティブかどうかの判断のため)
		private bool isActive_ = false;
		public bool IsActive {
			get => isActive_;
			set {
				SetProperty(ref this.isActive_, value);
				if (!isActive_) {
					ExecClose();
				}
			}
		}
		public event EventHandler IsActiveChanged;
		#endregion

		/// <summary>
		/// コマンド(温度設定やノズル移動)が実行可能かどうかをコマンドに伝搬させるためのプロパティ
		/// </summary>
		private bool CanCommandExecute {
			get => ctrl_.CanJobStart;
		}

		/// <summary>
		/// 移動の実行
		/// </summary>
		private void ExecuteMove() {
			if (ctrl_.CanJobStart && selectedItem_ != null) {
				switch (selectedItem_.Type) {
				case MoveElement.MoveType.XYMove:
					ctrl_.MoveXY(selectedItem_.PosX, selectedItem_.PosY, (uint)SpeedXY);
					break;
				case MoveElement.MoveType.XMove:
					ctrl_.MoveX(selectedItem_.PosX, (uint)SpeedXY);
					break;
				case MoveElement.MoveType.YMove:
					ctrl_.MoveY(selectedItem_.PosY, (uint)SpeedXY);
					break;
				case MoveElement.MoveType.ZMove:
					ctrl_.MoveZ(selectedItem_.PosZ, (uint)SpeedZ);
					break;
				case MoveElement.MoveType.EMove:
					ctrl_.MoveE(selectedItem_.PosE + ctrl_.PosE, (uint)SpeedE);
					break;
				}
				isMoved_ = true;
			}
		}

		/// <summary> 一応本タブアイテムが除去された場合の各種開放処理を入れておく </summary>
		public void Dispose() {
			ctrl_.PropertyChanged -= ChangedProperty;
			cmds_.CloseCommand.UnregisterCommand(CloseCmd);
			cmds_.DisConnectCommand.UnregisterCommand(CloseCmd);
		}

		/// <summary>
		/// このパラメータ画面から抜ける際の共通処理
		/// 温度設定を0にしておく、またこのパラメータ画面で移動処理を行っていたら、緊急停止コマンドを送付しXYZ座標を確定させておく
		/// </summary>
		private void ExecClose() {
			if (ctrl_.IsConnected) {
				if (isMoved_) {
					ctrl_.EmergencyStop();
				}
				if (DoHeatNozel) {
					ctrl_.TargetTempNozel = 0;
				}
				if (DoHeatBed) {
					ctrl_.TargetTempBed = 0;
				}
			}
			DoHeatBed = false;
			DoHeatNozel = false;
			isMoved_ = false;
		}

		/// <summary>
		/// 現在の設定の保存処理
		/// </summary>
		private void SaveCurrentData() {
			if (!HasErrors) {
				// パラメータの保存処理
				if (!string.IsNullOrEmpty(ctrl_.BaseFolderName)) {
					SaveParameter(parameterFileName_);
				}
			}
		}

		#region パラメータアクセッサ
		/// <summary>
		/// パラメータのチェックを行い、エラーがある場合、エラーコンテナに詰め込む
		/// これにより、エラーがあったテキストが赤くなり、エラー情報がポップアップで表示される
		/// </summary>
		private void Check() {
			var result = parameter_.IsValid();
			ErrorsContainer.ClearErrors();
			foreach (var data in result) {
				List<string> work = new List<string>();
				work.Add(data.Value);
				ErrorsContainer.SetErrors(data.Key, work);
				RaisePropertyChanged(data.Key);
			}
		}

		/// <summary>
		/// データの設定用メソッド
		/// データの比較を行い、変更があった場合、parameter側の属性を変更し、parameterのチェック処理を実施する
		/// </summary>
		private void SetProperty<T>(T value, [CallerMemberName] string propertyName = null) where T : IComparable {
			PropertyInfo property = parameter_.GetType().GetProperty(propertyName);
			if (property == null || (!property.CanRead && !property.CanWrite)) {
				return;
			}
			var target = (property.GetValue(parameter_)) as IComparable;
			if (target != null && value.CompareTo(target) != 0) {
				property.SetValue(parameter_, value);
				RaisePropertyChanged(propertyName);
				Check();
			}
		}

		public int CurrentTempNozel { get => ctrl_.CurrentTempNozel; }
		public int CurrentTempBed { get => ctrl_.CurrentTempBed; }
		public int TargetTempNozel {
			get => parameter_.TargetTempNozel;
			set {
				SetProperty(value);
				if (HeatNozel.CanExecute()) {
					HeatNozel.Execute();
				}
			}
		}
		public int TargetTempBed {
			get => parameter_.TargetTempBed;
			set {
				SetProperty(value);
				if (HeatBed.CanExecute()) {
					HeatBed.Execute();
				}
			}
		}
		public int SpeedXY {
			get => parameter_.SpeedXY;
			set => SetProperty(value);
		}
		public int SpeedZ {
			get => parameter_.SpeedZ;
			set => SetProperty(value);
		}
		public int SpeedE {
			get => parameter_.SpeedE;
			set => SetProperty(value);
		}
		private bool doHeatNozel_ = false;
		public bool DoHeatNozel {
			get => doHeatNozel_;
			set => SetProperty(ref doHeatNozel_, value);
		}
		private bool doHeatBed_ = false;
		public bool DoHeatBed {
			get => doHeatBed_;
			set => SetProperty(ref doHeatBed_, value);
		}

		public ObservableCollection<MoveElement> MoveData {
			get => parameter_.MoveData;
			set => parameter_.MoveData = value;
		}

		/// <summary>
		/// データグリッドの選択されている要素に関しての情報
		/// </summary>
		private MoveElement selectedItem_;
		public MoveElement SelectedItem {
			get => selectedItem_;
			set => SetProperty(ref selectedItem_, value);
		}

		public string CurrentPosition {
			get {
				if (ctrl_.IsConnected) {
					return string.Format(Properties.Resources.LbCurrentPosition2, ctrl_.PosX, ctrl_.PosY, ctrl_.PosZ);
				}
				else {
					return Properties.Resources.LbCurrentPosition1;
				}
			}
		}
		#endregion

		/// <summary>
		/// modelのプロパティ変更対応のイベント処理
		/// </summary>
		/// <param name="sender">オブジェクト名</param>
		/// <param name="eventArgs">イベント情報</param>
		private void ChangedProperty(object sender, PropertyChangedEventArgs eventArgs) {
			switch (eventArgs.PropertyName) {
			case Adventurer3Property.status:
				// ステータス更新がされたので、現在の温度を更新する
				RaisePropertyChanged(nameof(CurrentTempNozel));
				RaisePropertyChanged(nameof(CurrentTempBed));
				RaisePropertyChanged(nameof(CanCommandExecute));
				break;
			case Adventurer3Property.position:
				RaisePropertyChanged(nameof(CurrentPosition));
				break;
			case Adventurer3Property.connect:
				// 機器と接続or接続解除した段階で、いったんfalseに初期化する
				DoHeatBed = false;
				DoHeatNozel = false;
				// 既に機器に設定されている温度があるかもしれないのでそれも更新対象とする
				RaisePropertyChanged(nameof(TargetTempNozel));
				RaisePropertyChanged(nameof(TargetTempBed));
				// 接続・未接続が更新されたので、canJobStartも変更対象としておく
				RaisePropertyChanged(nameof(CanCommandExecute));
				RaisePropertyChanged(nameof(CurrentPosition));
				break;
			}
		}

		#region パラメータのファイルからの入出力
		/// <summary>
		/// パラメータファイルから設定を読み込む
		/// </summary>
		/// <param name="filename">パラメータファイル名</param>
		public void LoadParameter(string filename) {
			if (File.Exists(filename)) {
				using (var wStream = File.OpenRead(filename)) {
					var settings = new DataContractJsonSerializerSettings();
					var serializer = new DataContractJsonSerializer(typeof(ControlData), settings);
					var data1 = (ControlData)serializer.ReadObject(wStream);
					parameter_ = data1;
					RaisePropertyChanged(nameof(SpeedXY));
					RaisePropertyChanged(nameof(SpeedZ));
					RaisePropertyChanged(nameof(SpeedE));
					RaisePropertyChanged(nameof(MoveData));
					Check();
				}
			}
		}

		/// <summary>
		/// パラメータファイルに現在の設定を書き込む
		/// </summary>
		/// <param name="filename">パラメータファイル名</param>
		public void SaveParameter(string filename) {
			using (var stream1 = File.Create(filename)) {
				var serializer = new DataContractJsonSerializer(typeof(ControlData));
				serializer.WriteObject(stream1, parameter_);
			}
		}
		#endregion


		#region エラー対応用
		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		private ErrorsContainer<string> ErrorsContainer { get; }

		public bool HasErrors {
			get => ErrorsContainer.HasErrors;
		}

		public IEnumerable GetErrors(string propertyName) {
			return this.ErrorsContainer.GetErrors(propertyName);
		}
		#endregion
	}
}
