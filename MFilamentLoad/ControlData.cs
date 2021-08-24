using System;
using System.Runtime.Serialization;
using PrintControler.Controler;
using Take4.Common;

namespace PrintControler.MFilamentLoad {
	/// <summary>
	/// 状態遷移とプロセスを段階的に進めて行くための処理機能用インターフェース
	/// </summary>
	interface IProcessStep {
		/// <summary>
		/// 実行ステップを準に処理していく
		/// </summary>
		/// <returns>trueの場合、コマンドステップの終了</returns>
		bool CommandStep(ControlData data, Controler.IAdventurer3Controler controler);

		/// <summary>
		/// 現在の動作状況を表示する
		/// </summary>
		string nowStatus { get; }
	}

	/// <summary>
	/// IProcessStepを作成するためのファクトリークラス用インターフェース
	/// </summary>
	interface IProsessStepFactory {
		IProcessStep create(ControlData.CommandType type, ControlData.NozzleCleanType nozzle);
	}

	static internal class CommonFunc {
		internal static DateTime NextEventTime(double length, int speed) {
			return DateTime.Now + new TimeSpan(0, 0, (int)Math.Ceiling(length * 60.0 / speed));
		}
	}

	/// <summary>
	/// 各プロセスでの処理クラスを作成するファクトリー
	/// </summary>
	class StepFactory : IProsessStepFactory {
		public IProcessStep create(ControlData.CommandType type, ControlData.NozzleCleanType nozzle) {
			switch(type) {
			case ControlData.CommandType.EmergencyStop:
				return new EmergencyStopStep();
			case ControlData.CommandType.InsertFilament:
				return new InsertFilamentStep();
			case ControlData.CommandType.ExtractionFilament:
				return new ExtractionFilamentStep();
			case ControlData.CommandType.CleanNozzle:
				switch(nozzle) {
				case ControlData.NozzleCleanType.CleanupPreProcess:
					return new CleanupPreProcessStep();
				case ControlData.NozzleCleanType.FilamentCutPreProcess:
				case ControlData.NozzleCleanType.TubeInsertPreProcess:
					return new FilamentInExtStep(nozzle);
				case ControlData.NozzleCleanType.NozzleHighTemp:
				case ControlData.NozzleCleanType.NozzleLowTemp:
					return new NozzelTempStep(nozzle);
				case ControlData.NozzleCleanType.FilamentInsert:
					return new NozzleCleanInsertFilamentStep();
				}
				break;
			}
			return null;
		}
	}

	#region プロセスステップ用のクラス類

	/// <summary>
	/// フィラメント挿入用のプロセスステップ
	/// </summary>
	class InsertFilamentStep : IProcessStep {
		int nStep_ = 1;
		private DateTime targetTime;
		private bool isReachTargetTemp_ = false;
		private bool isReachMove_ = false;

		public bool CommandStep(ControlData data, Controler.IAdventurer3Controler controler) {
			switch (nStep_) {
			case 1:
				controler.TargetTempNozel = data.TargetHighTempNozel;
				isReachTargetTemp_ = false;
				isReachMove_ = false;
				controler.MoveE(controler.PosE + data.tubeLength_, (uint)data.SpeedEHigh);
				targetTime = CommonFunc.NextEventTime(data.tubeLength_, data.SpeedEHigh);
				nStep_++;
				break;
			case 2:
				if (controler.CurrentTempNozel >= data.TargetHighTempNozel) {
					isReachTargetTemp_ = true;
				}
				if (!isReachMove_ && targetTime < DateTime.Now) {
					controler.EmergencyStop();
					isReachMove_ = true;
				}
				if (isReachTargetTemp_ && isReachMove_) {
					nStep_++;
				}
				break;
			case 3:
				controler.MoveE(controler.PosE + data.tubeLength_, (uint)data.SpeedELow);
				nStep_++;
				break;
			}
			return false;
		}

		/// <summary>
		/// 現在の動作状況を表示する
		/// </summary>
		public string nowStatus {
			get {
				switch(nStep_) {
				case 2:
					return Properties.Resources.StInsertFilamentStep2;
				case 4:
					return Properties.Resources.StInsertFilamentStep4;
				}
				return "";
			}
		}
	}

	/// <summary>
	/// フィラメント抜去用プロセスステップ
	/// </summary>
	class ExtractionFilamentStep : IProcessStep {
		int nStep_ = 1;
		private DateTime targetTime;

		public bool CommandStep(ControlData data, IAdventurer3Controler controler) {
			switch (nStep_) {
			case 1:
				controler.TargetTempNozel = data.TargetHighTempNozel;
				nStep_++;
				break;
			case 2:
				if (controler.CurrentTempNozel >= data.TargetHighTempNozel) {
					nStep_++;
				}
				break;
			case 3:
				controler.MoveE(controler.PosE + data.preExtrudeLength_, (uint)data.SpeedELow);
				targetTime = CommonFunc.NextEventTime(data.preExtrudeLength_ , data.SpeedELow);
				nStep_++;
				break;
			case 4:
			case 6:
			case 9:
				if (targetTime < DateTime.Now) {
					controler.EmergencyStop();
					nStep_++;
				}
				break;
			case 5:
				controler.MoveE(controler.PosE - data.preExtrudeLength_, (uint)data.SpeedELow);
				targetTime = CommonFunc.NextEventTime(data.preExtrudeLength_, data.SpeedELow);
				nStep_++;
				break;
			case 7:
				controler.TargetTempNozel = data.TargetLowTempNozel;
				nStep_++;
				break;
			case 8:
				controler.MoveE(controler.PosE - data.tubeLength_ * 1.5, (uint)data.SpeedEHigh);
				targetTime = CommonFunc.NextEventTime(data.tubeLength_ * 1.5, data.SpeedEHigh);
				nStep_++;
				break;
			case 10:
				nStep_ = 0;
				return true;
			}
			return false;
		}

		/// <summary>
		/// 現在の動作状況を表示する
		/// </summary>
		public string nowStatus {
			get {
				switch (nStep_) {
				case 2:
					return Properties.Resources.StExtractionFilamentStep2;
				case 9:
					return Properties.Resources.StExtractionFilamentStep9;
				}
				return "";
			}
		}
	}

	/// <summary>
	/// 緊急停止用プロセスステップ
	/// </summary>
	class EmergencyStopStep : IProcessStep {
		public bool CommandStep(ControlData data, IAdventurer3Controler controler) {
			controler.EmergencyStop();
			controler.TargetTempNozel = 0;
			return true;
		}

		/// <summary>
		/// 現在の動作状況を表示する
		/// </summary>
		public string nowStatus { get => ""; }
	}

	/// <summary>
	/// ノズル清掃時の前処理用のプロセスステップ
	/// </summary>
	class CleanupPreProcessStep : IProcessStep {
		int nStep_ = 1;
		private DateTime targetTime;
		private bool isTargetTemp_ = false;
		private bool isReached_ = false;

		const double maxValue_ = 160.0;
		const double nozzleXPosition_ = 90.0;
		const double nozzelZDown_ = 120.0;

		public bool CommandStep(ControlData data, IAdventurer3Controler controler) {
			switch (nStep_) {
			case 1:
				// Z軸を上に持っていき、Z軸のリミットスイッチを押させる(1,2のケースの合わせ技)
				controler.Send(string.Format("G1 Z{0} F{1}", controler.PosZ + maxValue_, data.SpeedZ));
				nStep_++;
				break;
			case 2:
				if (controler.LimitZ) {
					controler.EmergencyStop();
					nStep_++;
				}
				break;
			case 3:
				// X軸を右に持っていき、X軸のリミットスイッチを押させる(3,4のケースの合わせ技)
				controler.Send(string.Format("G1 X{0} F{1}", controler.PosX + maxValue_, data.SpeedXY));
				nStep_++;
				break;
			case 4:
				if (controler.LimitX) {
					controler.EmergencyStop();
					nStep_++;
				}
				break;
			case 5:
				// ノズル温度を高温にする。
				// またノズルをX軸中央に持ってくる。(6で時間調整をする)
				controler.TargetTempNozel = data.TargetHighTempNozel;
				isTargetTemp_ = false;
				controler.Send(string.Format("G1 X{0} F{1}", controler.PosX - nozzleXPosition_, data.SpeedXY));
				targetTime = CommonFunc.NextEventTime(nozzleXPosition_, data.SpeedXY);
				nStep_++;
				break;
			case 6:
			case 10:
			case 12:
				// G1命令の時間調整
				if (targetTime < DateTime.Now) {
					controler.EmergencyStop();
					nStep_++;
				}
				break;
			case 7:
				// Z軸を清掃機具が差し込めるぐらい下げる。(8で時間調整する)
				controler.Send(string.Format("G1 Z{0} F{1}", controler.PosZ - nozzelZDown_, data.SpeedZ));
				isReached_ = false;
				targetTime = CommonFunc.NextEventTime(nozzelZDown_, data.SpeedZ);
				nStep_++;
				break;
			case 8:
				// 7の時間調整と、ノズル温度が指定温度になっているかを確認。
				if (controler.CurrentTempNozel >= data.TargetHighTempNozel) {
					isTargetTemp_ = true;
				}
				if (!isReached_ && targetTime < DateTime.Now) {
					controler.EmergencyStop();
					isReached_ = true;
				}
				if (isReached_ && isTargetTemp_) {
					nStep_++;
				}
				break;
			case 9:
				// フィラメントを遅めにちょっとだけ出す。(10で時間調整をする)
				controler.MoveE(controler.PosE + data.preExtrudeLength_, (uint)data.SpeedELow);
				targetTime = CommonFunc.NextEventTime(data.preExtrudeLength_, data.SpeedELow);
				nStep_++;
				break;
			case 11:
				// フィラメントを遅めにちょっとだけ引き抜く。(12で時間調整をする)
				controler.MoveE(controler.PosE - data.preExtrudeLength_, (uint)data.SpeedELow);
				targetTime = CommonFunc.NextEventTime(data.preExtrudeLength_, data.SpeedELow);
				nStep_++;
				break;
			case 13:
				// フィラメントを高速でガイドチューブ内に引っ込める。
				controler.MoveE(controler.PosE - data.headBaseInnerLength_, (uint)data.SpeedEHigh);
				targetTime = CommonFunc.NextEventTime(data.headBaseInnerLength_, data.SpeedEHigh);
				nStep_++;
				break;
			case 14:
				// 時間調整と、終了。
				if (targetTime < DateTime.Now) {
					controler.EmergencyStop();
					nStep_++;
					return true;
				}
				break;
			}
			return false;
		}

		/// <summary>
		/// 現在の動作状況を表示する
		/// </summary>
		public string nowStatus {
			get {
				switch (nStep_) {
				case 2:
					return Properties.Resources.StCleanupPreProcessStep2;
				case 4:
					return Properties.Resources.StCleanupPreProcessStep4;
				case 6:
					return Properties.Resources.StCleanupPreProcessStep6;
				case 8:
					return Properties.Resources.StCleanupPreProcessStep8;
				case 14:
					return Properties.Resources.StCleanupPreProcessStep14;
				}
				return "";
			}
		}
	}

	/// <summary>
	/// ノズル温度の設定
	/// </summary>
	class NozzelTempStep : IProcessStep {
		ControlData.NozzleCleanType type_;
		public NozzelTempStep(ControlData.NozzleCleanType type) {
			type_ = type;
		}

		public bool CommandStep(ControlData data, IAdventurer3Controler controler) {
			switch(type_) {
			case ControlData.NozzleCleanType.NozzleHighTemp:
				controler.TargetTempNozel = data.TargetHighTempNozel;
				break;
			case ControlData.NozzleCleanType.NozzleLowTemp:
				controler.TargetTempNozel = data.TargetLowTempNozel;
				break;
			}
			return true;
		}

		/// <summary>
		/// 現在の動作状況を表示する
		/// </summary>
		public string nowStatus { get => ""; }
	}

	/// <summary>
	/// フィラメントカット、ガイドチューブ挿入の前処理用プロセスステップ
	/// </summary>
	class FilamentInExtStep : IProcessStep {
		int nStep_ = 1;
		private DateTime targetTime;

		ControlData.NozzleCleanType type_;
		public FilamentInExtStep(ControlData.NozzleCleanType type) {
			type_ = type;
		}

		public bool CommandStep(ControlData data, IAdventurer3Controler controler) {
			switch (nStep_) {
			case 1:
				if (type_ == ControlData.NozzleCleanType.FilamentCutPreProcess) {
					controler.MoveE(controler.PosE + data.headBaseInnerLength_, (uint)data.SpeedELow);
				}
				else {
					controler.MoveE(controler.PosE - data.headBaseInnerLength_, (uint)data.SpeedELow);
				}
				targetTime = CommonFunc.NextEventTime(data.headBaseInnerLength_, data.SpeedELow);
				nStep_++;
				break;
			case 2:
				if (targetTime < DateTime.Now) {
					controler.EmergencyStop();
					nStep_++;
					return true;
				}
				break;
			}
			return false;
		}


		/// <summary>
		/// 現在の動作状況を表示する
		/// </summary>
		public string nowStatus {
			get {
				if (nStep_ == 2) {
					return type_ == ControlData.NozzleCleanType.FilamentCutPreProcess ?
						Properties.Resources.StFilamentInExtStep21:
						Properties.Resources.StFilamentInExtStep22;
				}
				return "";
			}
		}
	}

	/// <summary>
	/// ノズル清掃時のフィラメント挿入用プロセスステップ
	/// </summary>
	class NozzleCleanInsertFilamentStep : IProcessStep {
		int nStep_ = 1;
		const double nozzleDown_ = 121.0;

		public bool CommandStep(ControlData data, IAdventurer3Controler controler) {
			switch(nStep_) {
			case 1:
				// Z軸を上に持っていき、Z軸のリミットスイッチを押させる(1,2のケースの合わせ技)
				controler.Send(string.Format("G1 Z{0} F{1}", controler.PosZ + nozzleDown_, data.SpeedZ));
				nStep_++;
				break;
			case 2:
				if (controler.LimitZ) {
					controler.EmergencyStop();
					nStep_++;
				}
				break;
			case 3:
				controler.TargetTempNozel = data.TargetHighTempNozel;
				nStep_++;
				break;
			case 4:
				if (controler.CurrentTempNozel >= data.TargetHighTempNozel) {
					nStep_++;
				}
				break;
			case 5:
				controler.MoveE(controler.PosE + data.tubeLength_, (uint)data.SpeedELow);
				nStep_++;
				break;
			}
			return false;
		}

		/// <summary>
		/// 現在の動作状況を表示する
		/// </summary>
		public string nowStatus {
			get {
				switch (nStep_) {
				case 2:
					return Properties.Resources.StNozzleCleanInsertFilamentStep2;
				case 4:
					return Properties.Resources.StNozzleCleanInsertFilamentStep4;
				case 6:
					return Properties.Resources.StNozzleCleanInsertFilamentStep6;
				}
				return "";
			}
		}
	}
	#endregion

	/// <summary>
	/// フィラメントの挿入・抜去、ノズル洗浄の制御機能
	/// </summary>
	[DataContract]
	internal class ControlData : ICheckableData, IDisposable {
		internal enum CommandType {
			Ready,  // 何もやっていない状態
			InsertFilament,    // フィラメント挿入動作
			ExtractionFilament,   // フィラメント抜去動作
			CleanNozzle,    // ノズル清掃
			EmergencyStop,  // 緊急停止
		}

		/// <summary>
		/// ノズルの洗浄方法種類の指定
		/// </summary>
		internal enum NozzleCleanType {
			CleanupPreProcess,
			NozzleHighTemp,
			NozzleLowTemp,
			FilamentCutPreProcess,
			TubeInsertPreProcess,
			FilamentInsert,
		}

		Controler.IAdventurer3Controler ctrl_;
		internal IProsessStepFactory factory_ = new StepFactory();

		public ControlData(Controler.IAdventurer3Controler ctrl) {
			ctrl_ = ctrl;
			ctrl_.PropertyChanged += ChangedProperty;
		}

		public ControlData() {
		}

		void IDisposable.Dispose() {
			if (ctrl_ != null) {
				ctrl_.PropertyChanged -= ChangedProperty;
			}
		}

		private CommandType current_ = CommandType.Ready;
		private IProcessStep step_;

		private void StepByStep() {
			if (step_ != null) {
				if (step_.CommandStep(this, ctrl_)) {
					current_ = CommandType.Ready;
					step_ = null;
				}
			}
		}

		/// <summary>
		/// コマンドの実行
		/// </summary>
		public void ExecuteCommand(CommandType action, NozzleCleanType CleanType) {
			if (ctrl_.IsConnected && ctrl_.CanJobStart) {
				if (action == CommandType.EmergencyStop || (current_ == CommandType.Ready && action != CommandType.Ready)) {
					current_ = action;
					step_ = factory_.create(action, CleanType);
					StepByStep();
				}
			}
		}

		/// <summary>
		/// コマンドの停止
		/// </summary>
		public void StopCommand() {
			current_ = CommandType.Ready;
			step_ = null;
		}

		/// <summary>
		/// modelのプロパティ変更対応のイベント処理
		/// </summary>
		/// <param name="sender">オブジェクト名</param>
		/// <param name="eventArgs">イベント情報</param>
		private void ChangedProperty(object sender, System.ComponentModel.PropertyChangedEventArgs eventArgs) {
			switch (eventArgs.PropertyName) {
			case Controler.Adventurer3Property.status:
				StepByStep();
				break;
			case Controler.Adventurer3Property.position:
				break;
			case Controler.Adventurer3Property.connect:
				break;
			}
		}

		#region パラメータ類
		int targetLowTempNozel_ = 70;
		int targetHighTempNozel_ = 220;
		int lowSpeed_ = 180;
		int highSpped_ = 2400;
		int xySpeed_ = 3000;
		int zSpeed_ = 420;

		/// <summary>
		/// チューブの長さ
		/// </summary>
		[DataMember(Name ="TubeLength")]
		internal double tubeLength_ = 470.0;
		/// <summary>
		/// ちょっとだしのための長さ
		/// </summary>
		[DataMember(Name ="PreExtrudeLength")]
		internal double preExtrudeLength_ = 10.0;

		/// <summary>
		/// ヘッドベース内のフィラメントの長さ
		/// </summary>
		[DataMember(Name = "HeadBaseInnerLength")]
		internal double headBaseInnerLength_ = 80.0;

		[DataMember]
		public int TargetLowTempNozel {
			get => targetLowTempNozel_;
			set => SetProperty(ref targetLowTempNozel_, value);
		}

		[DataMember]
		public int TargetHighTempNozel {
			get => targetHighTempNozel_;
			set => SetProperty(ref targetHighTempNozel_, value);
		}

		[DataMember]
		public int SpeedELow {
			get => lowSpeed_;
			set => SetProperty(ref lowSpeed_, value);
		}

		[DataMember]
		public int SpeedEHigh {
			get => highSpped_;
			set => SetProperty(ref highSpped_, value);
		}

		[DataMember]
		public int SpeedXY {
			get => xySpeed_;
			set => SetProperty(ref xySpeed_, value);
		}

		[DataMember]
		public int SpeedZ {
			get => zSpeed_;
			set => SetProperty(ref zSpeed_, value);
		}

		void SetProperty<T>(ref T target, T source) {
			target = source;
		}

		/// <summary>
		/// 設定値が正しいかどうかの判断
		/// </summary>
		/// <returns>エラーがあった場合メンバー名+メッセージが出力される</returns>
		public System.Collections.Generic.Dictionary<string, string> IsValid() {
			System.Collections.Generic.Dictionary<string, string> result = new System.Collections.Generic.Dictionary<string, string>();
			Take4.Common.Checker.RangeCheck(SpeedELow, 1, 6000, ref result, nameof(SpeedELow));
			Take4.Common.Checker.RangeCheck(SpeedEHigh, 1, 6000, ref result, nameof(SpeedEHigh));
			Take4.Common.Checker.RangeCheck(SpeedXY, 1, 9000, ref result, nameof(SpeedXY));
			Take4.Common.Checker.RangeCheck(SpeedZ, 1, 3000, ref result, nameof(SpeedZ));
			Take4.Common.Checker.RangeCheck(TargetLowTempNozel, 0, 240, ref result, nameof(TargetLowTempNozel));
			Take4.Common.Checker.RangeCheck(TargetHighTempNozel, 0, 240, ref result, nameof(TargetHighTempNozel));
			return result;
		}
		#endregion

		/// <summary>
		/// データの設定
		/// </summary>
		/// <param name="data">設定元のデータ</param>
		public void set(ICheckableData data) {
			var value = data as ControlData;
			if (value != null) {
				TargetHighTempNozel = value.TargetHighTempNozel;
				TargetLowTempNozel = value.TargetLowTempNozel;
				SpeedEHigh = value.SpeedEHigh;
				SpeedELow = value.SpeedELow;
				SpeedXY = value.SpeedXY;
				SpeedZ = value.SpeedZ;
				preExtrudeLength_ = value.preExtrudeLength_;
				tubeLength_ = value.tubeLength_;
				headBaseInnerLength_ = value.headBaseInnerLength_;
			}
		}

		/// <summary>
		/// 現在のコマンドのステータス
		/// </summary>
		public string NowCommandStatus {
			get => step_ != null ? step_.nowStatus : "";
		}
	}
}
