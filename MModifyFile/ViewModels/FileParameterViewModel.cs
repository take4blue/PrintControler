using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using PrintControler.Controler;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Take4.Common;
using Take4.Translator;

namespace PrintControler.MModifyFile.ViewModels {
	public class FileParameterViewModel : BindableBase, INotifyDataErrorInfo , IDisposable {
		private ToAdventurer3Parameter parameter_;
		private IApplicationCommands cmds_;
		private IAdventurer3Controler model_;
		private IEventAggregator ea_;
		private IRegionManager rm_;

		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		#region コマンド類
		private DelegateCommand<Object> CloseCmd { get; set; }
		public DelegateCommand<string[]> DropCommand { get; private set; }
		#endregion

		public string HeaderText { get; private set; }

		private string parameterFileName_;
		/// <summary>
		/// 変更後のファイル
		/// </summary>
		private TemporaryFile tempFile_ = new TemporaryFile();
		private string addLabel_;

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="model">コントローラーモデル</param>
		/// <param name="cmds">コマンド</param>
		public FileParameterViewModel(IAdventurer3Controler model, IApplicationCommands cmds, IEventAggregator ea, IRegionManager rm) {
			model_ = model;
			cmds_ = cmds;
			ea_ = ea;
			rm_ = rm;

			HeaderText = Properties.Resources.OutputParameter;

			parameter_ = new ToAdventurer3Parameter();
			this.ErrorsContainer = new ErrorsContainer<string>(
				x => this.ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(x)));

			CloseCmd = new DelegateCommand<Object>(x => DoClose(x));
			DropCommand = new DelegateCommand<string[]>(
				x => DropAction(x),
				x => {
					var file = Checker.CanReadDcodeFile(x, model_.CanJobStart);
					return file != null ? true : false;
				});

			model_.PropertyChanged += ChangedProperty;
			cmds_.CloseCommand.RegisterCommand(CloseCmd);

			ea_.GetEvent<PrintEvent>().Subscribe(PrintExec, ThreadOption.PublisherThread, false,
				x => x.Type == PrintData.TargetType.ModifyToAdventure3);    // Adventurer3用へのデータ編集イベントを受け取る

			parameterFileName_ = model.BaseFolderName + @"\parameter.json";
			LoadParameter(parameterFileName_);
		}

		private void DoClose(Object target) {
			tempFile_.Delete();
			if (!HasErrors) {
				// パラメータの保存処理
				if (!string.IsNullOrEmpty(model_.BaseFolderName)) {
					SaveParameter(parameterFileName_);
				}
			}
		}

		public void Dispose() {
			model_.PropertyChanged -= ChangedProperty;
			cmds_.CloseCommand.UnregisterCommand(CloseCmd);
			tempFile_.Dispose();
		}

		#region ドロップ処理類
		/// <summary>
		/// ドロップイベントの処理ルーチン
		/// </summary>
		/// <param name="parameter">ドロップされたファイル名</param>
		private void DropAction(string [] parameter) {
			if (!HasErrors) {
				var file = Checker.CanReadDcodeFile(parameter, model_.CanJobStart);
				PrintStart(file, file);
			}
		}

		/// <summary>
		/// Adventurer3用にファイルを修正し、印刷処理にファイルを送信する
		/// </summary>
		/// <param name="originalFileName">もともとのファイル名</param>
		/// <param name="realFilename">実際に処理をするファイル名</param>
		private void PrintStart(string originalFilename, string realFilename) {
			if (!HasErrors && !string.IsNullOrEmpty(realFilename) && File.Exists(realFilename)) {
				addLabel_ = "";
				// 変更処理を行い、印刷ジョブにファイルを渡す
				tempFile_.Delete();
				bool execPrintJob = false;
				using (var input = File.OpenRead(realFilename)) {
					using (var output = File.Create(tempFile_.FileName)) {
						var modifier = new ToAdventurer3();
						modifier.Parameter.Set(parameter_);
						if (!modifier.Modify(input, output)) {
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
					ea_.GetEvent<PrintEvent>().Publish(new PrintData() { OrignalFileName = originalFilename, RealFileName = tempFile_.FileName });
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

		/// <summary>
		/// イベントアグリゲーターで来た印刷イベントを処理
		/// </summary>
		/// <param name="message">印刷のための情報</param>
		private void PrintExec(PrintData message) {
			rm_.RequestNavigate(Adventurer3Property.tabRegionName, nameof(Views.FileParameter));		// 自分自身にページ遷移
			PrintStart(message.OrignalFileName ?? message.RealFileName, message.RealFileName);
		}

		#region ToAdventurer3Parameterに対するパラメータアクセッサ
		/// <summary>
		/// パラメータのチェックを行い、エラーがある場合、エラーコンテナに詰め込む
		/// </summary>
		private void Check() {
			var result = parameter_.IsValid(false);
			ErrorsContainer.ClearErrors();
			foreach(var data in result) {
				List<string> work = new List<string>();
				work.Add(data.Value);
				ErrorsContainer.SetErrors(data.Key, work);
				RaisePropertyChanged(data.Key);
			}
		}

		/// <summary>
		/// データの設定用メソッド
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

		/// <summary>
		/// ブリム速度選択用リストボックスのバイディング先
		/// </summary>
		public int BrimSpeedTypeValue {
			get {
				switch (parameter_.BrimSpeedTypeValue) {
				case ToAdventurer3Parameter.BrimSpeedType.NoChange:
					return 0;
				case ToAdventurer3Parameter.BrimSpeedType.Absolute:
					return 1;
				case ToAdventurer3Parameter.BrimSpeedType.Ratio:
					return 2;
				}
				return 0;
			}
			set {
				switch (value) {
				case 1:
					parameter_.BrimSpeedTypeValue = ToAdventurer3Parameter.BrimSpeedType.Absolute;
					break;
				case 2:
					parameter_.BrimSpeedTypeValue = ToAdventurer3Parameter.BrimSpeedType.Ratio;
					break;
				case 0:
				default:
					parameter_.BrimSpeedTypeValue = ToAdventurer3Parameter.BrimSpeedType.NoChange;
					break;
				}
				RaisePropertyChanged();
			}
		}

		public bool EnclosureFanOn {
			set => SetProperty(value);
			get => parameter_.EnclosureFanOn;
		}

		public byte MotorX {
			set => SetProperty(value);
			get => parameter_.MotorX;
		}

		public byte MotorY {
			set => SetProperty(value);
			get => parameter_.MotorY;
		}

		public byte MotorZ {
			set => SetProperty(value);
			get => parameter_.MotorZ;
		}

		public byte MotorA {
			set => SetProperty(value);
			get => parameter_.MotorA;
		}

		public double PlayRemovalLength {
			set => SetProperty(value);
			get => parameter_.PlayRemovalLength;
		}

		public double OffsetZ {
			set => SetProperty(value);
			get => parameter_.OffsetZ;
		}

		public int BrimSpeed {
			set => SetProperty(value);
			get => parameter_.BrimSpeed;
		}

		public int BrimSpeedRatio {
			set => SetProperty(value);
			get => parameter_.BrimSpeedRatio;
		}

		public int BrimExtrudeRatio {
			set => SetProperty(value);
			get => parameter_.BrimExtrudeRatio;
		}
		#endregion

		#region パラメータのファイルからの入出力
		/// <summary>
		/// パラメータファイルから設定を読み込む
		/// </summary>
		/// <param name="filename">パラメータファイル名</param>
		public void LoadParameter(string filename) {
			if (File.Exists(filename)) {
				using (var wStream = File.OpenRead(filename)) {
					var settings = new DataContractJsonSerializerSettings();
					var serializer = new DataContractJsonSerializer(typeof(ToAdventurer3Parameter), settings);
					var data1 = (ToAdventurer3Parameter)serializer.ReadObject(wStream);
					parameter_.Set(data1);
					RaisePropertyChanged(nameof(BrimSpeedTypeValue));
					RaisePropertyChanged(nameof(EnclosureFanOn));
					RaisePropertyChanged(nameof(MotorX));
					RaisePropertyChanged(nameof(MotorY));
					RaisePropertyChanged(nameof(MotorZ));
					RaisePropertyChanged(nameof(MotorA));
					RaisePropertyChanged(nameof(PlayRemovalLength));
					RaisePropertyChanged(nameof(OffsetZ));
					RaisePropertyChanged(nameof(BrimSpeed));
					RaisePropertyChanged(nameof(BrimSpeedRatio));
					RaisePropertyChanged(nameof(BrimExtrudeRatio));
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
				var serializer = new DataContractJsonSerializer(typeof(ToAdventurer3Parameter));
				serializer.WriteObject(stream1, parameter_);
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
			case Adventurer3Property.status:           // 機器のステータス更新がされたので、表示用のステータスを更新対象とする
				RaisePropertyChanged(nameof(DropAreaLabel));
				break;
			}
		}

		#region エラー対応用
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
