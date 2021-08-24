using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace PrintControler.Controler.Test {
	/// <summary>
	/// テストサンプル
	/// 機器と接続状態にするには、startStop()で接続・未接続状態を切り替えます。
	/// </summary>
	public class TestControler : IAdventurer3Controler {
		bool isConnected_ = false;
		public bool IsConnected => isConnected_;

		public bool LimitX => false;

		public bool LimitY => false;

		public bool LimitZ => false;

		MachineStatus status_ = MachineStatus.Ready;
		public MachineStatus Status => status_;
		Random rnd = new Random();
		public int CurrentTempBed => rnd.Next(0, 100);

		public int CurrentTempNozel => rnd.Next(0, 250);


		#region 座標系の情報(Adventurer3Property.positionでの通知対象)
		public double PosX => rnd.Next(0, 250);
		public double PosY => rnd.Next(0, 250);
		public double PosZ => rnd.Next(0, 250);
		public double PosE => rnd.Next(0, 250);
		#endregion

		public int TargetTempBed {
			get => 10;
			set {
				if (CanJobStart) {
					if (CanJobStart) {
						Send(string.Format("M140 S{0} T0", value));
					}
				}
			}
		}
		public int TargetTempNozel {
			get => 20;
			set {
				if (CanJobStart) {
					Send(string.Format("M104 S{0} T0", value));
				}
			}
		}

		int sdProgress_ = 0;
		public int SdProgress => sdProgress_;

		public int SdMax => 100;

		public event PropertyChangedEventHandler PropertyChanged;

		const int statusCheckInterval_ = 1000;
		Task sync_;
		/// <summary>
		/// ステータス更新用のバックグラウンド処理の開始
		/// </summary>
		private void startUpdate() {

			if (sync_ != null) {
				if (sync_.IsCanceled || sync_.IsCompleted) {
					sync_.Dispose();
					sync_ = null;
				}
			}
			if (sync_ == null) {
				sync_ = Task.Run(() => {
					while (isConnected_) {
						// ロック中の場合、データ送信されていることになるので、今回の更新はパスする。
						PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.status));
						Thread.Sleep(statusCheckInterval_);
						switch (status_) {
						case MachineStatus.Building:
							sdProgress_ += 10;
							if (sdProgress_ >= 100) {
								status_ = MachineStatus.Ready;
								sdProgress_ = 0;
							}
							break;
						}
					}
				});
			}
		}

		public void startStop() {
			isConnected_ = isConnected_ ? false : true;
			if (isConnected_) {
				startUpdate();
			}
			PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.status));
			PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.connect));
		}

		public string Send(string msg) {
			return "ok";
		}

		/// <summary>
		/// sendで返ってきた値がOKだったかどうかを調べる
		/// </summary>
		/// <param name="retVal">sendの返却値</param>
		/// <returns>trueの場合OKな返却値</returns>
		public bool IsOK(string retVal) {
			return true;
		}

		public void StopJob() {
			switch (status_) {
			case MachineStatus.Building:
				sdProgress_ = 100;
				break;
			}
		}
		public bool CanJobStart { get => IsConnected && Status == MachineStatus.Ready; }
		public string BaseFolderName { get; set; }

		/// <summary>
		/// XYとFのそれぞれの値のチェック。
		/// チェックしない項目にはf以外0を入れる
		/// </summary>
		bool isValid(double x, double y, uint f) {
			if (f == 0 || f > 9999) {
				return false;
			}
			if (-75.0 < x || x > 80.0) {
				return false;
			}
			if (-76.0 < y || y > 75.0) {
				return false;
			}
			return true;
		}

		/// <summary>
		/// XY軸の移動(canJobStart==trueの場合のみ動作)
		/// X,Yは移動可能範囲内のみ動作
		/// </summary>
		/// <param name="f">移動スピード(mm/分):1以上の場合動作</param>
		public void MoveXY(double x, double y, uint f) {
			if (CanJobStart) {
				if (isValid(x, y, f)) {
					EmergencyStop();
					Send(string.Format("G1 X{0} Y{1} F{2}", x, y, f));
				}
			}
		}

		/// <summary>
		/// X軸の移動(canJobStart==trueの場合のみ動作)
		/// Xは移動可能範囲内のみ動作
		/// </summary>
		/// <param name="f">移動スピード(mm/分):1以上の場合動作</param>
		public void MoveX(double x, uint f) {
			if (CanJobStart) {
				if (isValid(x, 0.0, f)) {
					EmergencyStop();
					Send(string.Format("G1 X{0} F{1}", x, f));
				}
			}
		}

		/// <summary>
		/// Y軸の移動(canJobStart==trueの場合のみ動作)
		/// Yは移動可能範囲内のみ動作
		/// </summary>
		/// <param name="f">移動スピード(mm/分):1以上の場合動作</param>
		public void MoveY(double y, uint f) {
			if (CanJobStart) {
				if (isValid(0.0, y, f)) {
					EmergencyStop();
					Send(string.Format("G1 Y{0} F{1}", y, f));
				}
			}
		}

		/// <summary>
		/// Z軸の移動(canJobStart==trueの場合のみ動作)
		/// Zは移動可能範囲内のみ動作
		/// </summary>
		/// <param name="f">移動スピード(mm/分):1以上の場合動作</param>
		public void MoveZ(double z, uint f) {
			if (CanJobStart) {
				if (-0.5 < z || z > 154.0) {
					return;
				}
				if (f == 0 || f > 1200) {
					return;
				}
				EmergencyStop();
				Send(string.Format("G1 Z{0} F{1}", z, f));
			}
		}

		/// <summary>
		/// エクストルーダーの送り出し(canJobStart==trueの場合のみ動作)
		/// ノズル先端までフィラメントが詰まっていてノズル温度が低い場合、エクストルーダー部からノック音が出る可能性があるので注意
		/// 上記に対するチェックはしていない。
		/// </summary>
		/// <param name="f">送り出しスピード(mm/分):1以上の場合動作</param>
		public void MoveE(double e, uint f) {
			if (CanJobStart) {
				EmergencyStop();
				Send(string.Format("G1 E{0} F{1}", e, f));
			}
		}

		/// <summary>
		/// 緊急停止(M112を送信する)
		/// </summary>
		public void EmergencyStop() {
			Send("M112");
		}
	}
}
