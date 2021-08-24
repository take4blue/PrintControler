using PrintControler.Controler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Prism.Logging;
using System.IO;
using System.Reflection;

namespace PrintControler {
	/// <summary>
	/// Adventurer3テスト用スタブ
	/// </summary>
	internal class Adventurer3TestStub : IControler {
		ILoggerFacade logger_;
		private Mutex mut_ = new Mutex();

		public Adventurer3TestStub(ILoggerFacade logger) {
			logger_ = logger;
		}
		bool isConnected_ = false;
		public bool IsConnected => isConnected_;

		public bool LimitX => false;

		public bool LimitY => false;

		public bool LimitZ => false;

		MachineStatus status_ = MachineStatus.Ready;
		public MachineStatus Status => status_;
		Random rnd = new Random();
		public int CurrentTempBed { get; private set; }

		public int CurrentTempNozel { get; private set; }

		#region 座標系の情報(Adventurer3Property.positionでの通知対象)
		public double PosX => rnd.Next(0, 250);
		public double PosY => rnd.Next(0, 250);
		public double PosZ => rnd.Next(0, 250);
		public double PosE => rnd.Next(0, 250);
		#endregion

		int targetTempBed_ = 0;
		public int TargetTempBed {
			get => targetTempBed_;
			set {
				if (CanJobStart) {
					if (CanJobStart) {
						Send(string.Format("M140 S{0} T0", value));
						targetTempBed_ = value;
					}
				}
			}
		}

		int targetTempNozel_ = 0;
		public int TargetTempNozel {
			get => targetTempNozel_;
			set {
				if (CanJobStart) {
					Send(string.Format("M104 S{0} T0", value));
					targetTempNozel_ = value;
				}
			}
		}

		int sdProgress_ = 0;
		public int SdProgress => sdProgress_;

		public int SdMax => 100;

		long remain_;
		long transfer_;

		public event PropertyChangedEventHandler PropertyChanged;

		public long RemainTransferByte => remain_;

		const int statusCheckInterval_ = 300;
		public long TransferByte => transfer_;
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
						if (mut_.WaitOne(0)) {
							// ロック中の場合、データ送信されていることになるので、今回の更新はパスする。
							PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.status));
							PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.position));
							if (targetTempNozel_ > CurrentTempNozel) {
								CurrentTempNozel++;
							}
							else if (targetTempNozel_ < CurrentTempNozel) {
								CurrentTempNozel--;
							}
							if (targetTempBed_ > CurrentTempBed) {
								CurrentTempBed++;
							}
							else if (targetTempBed_ < CurrentTempBed) {
								CurrentTempBed--;
							}
							switch (status_) {
							case MachineStatus.Building:
								sdProgress_ += 10;
								if (sdProgress_ >= 100) {
									status_ = MachineStatus.Ready;
									sdProgress_ = 0;
								}
								break;
							}
							mut_.ReleaseMutex();
						}
						else {
							PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.status));
						}
						Thread.Sleep(statusCheckInterval_);
					}
				});
			}
		}

		public bool ClickConnect(IPAddress connectIP) {
			isConnected_ = isConnected_ ? false : true;
			if (isConnected_) {
				startUpdate();
			}
			PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.status));
			PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.connect));
			return true;
		}

		const int blockSize_ = 4096;
		public async Task<bool> ExecuteGFile(string sendfile, string targetName) {
			if (isConnected_) {
				mut_.WaitOne();
				var result = await Task.Run(() => {
					using (var fp = new FileStream(sendfile, FileMode.Open, FileAccess.Read)) {
						transfer_ = fp.Length;
						status_ = MachineStatus.Transfer;
						var remain = transfer_;
						remain_ = remain;
						for (long i = 0; i < fp.Length && remain_ > 0; i += blockSize_, remain_ -= blockSize_, remain -= blockSize_) {
							PropertyChanged(this, new PropertyChangedEventArgs(Adventurer3Property.status));
							Thread.Sleep(10);
						}
						sdProgress_ = 0;
						transfer_ = 0; 
						if (remain == remain_) {
							status_ = MachineStatus.Building;
							return true;
						}
						else {
							status_ = MachineStatus.Ready;
							return false;
						}
					}
				});
				mut_.ReleaseMutex();
				return result;
			}
			return false;
		}

		public List<IPAddress> SearchIP() {
			List<IPAddress> result = new List<IPAddress>();
			return result;
		}

		public string Send(string msg) {
			logger_.Log(string.Format("{0} {1}", MethodBase.GetCurrentMethod().Name, msg), Category.Debug, Priority.Medium);
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
			case MachineStatus.Transfer:
				remain_ = 0;
				break;
			}
		}
		public bool CanJobStart { get => IsConnected && Status == MachineStatus.Ready; }
		public string ConnectedIp { get => IsConnected ? "192.168.10.10" : ""; }
		public string BaseFolderName { get; set; }

		public BitmapImage Image { get; }

		public void Led(bool value) {
		}
		public void StreamAction(bool value) {
			logger_.Log(string.Format("{0} {1}", MethodBase.GetCurrentMethod().Name, value), Category.Debug, Priority.Medium);
		}

		/// <summary>
		/// XYとFのそれぞれの値のチェック。
		/// チェックしない項目にはf以外0を入れる
		/// </summary>
		bool isValid(double x, double y, uint f) {
			if (f == 0 || f > 9999) {
				return false;
			}
			if (-75.0 > x || x > 80.0) {
				return false;
			}
			if (-76.0 > y || y > 75.0) {
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
				if (-0.5 > z || z > 154.0) {
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