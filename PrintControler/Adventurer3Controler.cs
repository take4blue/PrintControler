using MjpegProcessor;
using PrintControler.Controler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Take4.AControler;

namespace PrintControler {
	/// <summary>
	/// PrintControler内で使えるモデルのインターフェース
	/// IAdventurer3Controlerを継承し、ほかに必要なものを入れてある。
	/// 'またマシンの状態に関する情報、adventurer3Statusという名前でプロパティ変更のイベントが発生する。'
	/// イメージが更新された場合、adventurer3Imageという名前でプロパティ変更のイベントが発生する。
	/// </summary>
	internal interface IControler : IAdventurer3Controler {
		/// <summary>
		/// 接続ボタンが押された時の動作
		/// 未接続の場合は、接続をする。接続中の場合は、接続解除をする。
		/// </summary>
		bool ClickConnect(IPAddress connectIP);
		/// <summary>
		/// Gコードファイルを印刷する
		/// 中身ではファイルの転送及び印刷を行っている。
		/// </summary>
		/// <param name="sendfile">Gコードファイル</param>
		Task<bool> ExecuteGFile(string sendfile, string targetName = null);
		/// <summary>
		/// Adventurer3のIPをサーチする
		/// 中身的にはAdventurer3.searchIPのラッパー
		/// </summary>
		List<IPAddress> SearchIP();
		/// <summary>
		/// 残りの転送量
		/// </summary>
		long RemainTransferByte { get; }
		/// <summary>
		/// 転送するデータの量
		/// </summary>
		long TransferByte { get; }
		/// <summary>
		/// 接続先のIPアドレス名称
		/// </summary>
		string ConnectedIp { get; }
		/// <summary>
		/// カメラの映像
		/// </summary>
		BitmapImage Image { get; }
		/// <summary>
		/// LEDの点灯/消灯
		/// </summary>
		/// <param name="value">trueの場合点灯</param>
		void Led(bool value);
		/// <summary>
		/// 映像配信のON/OFF
		/// </summary>
		/// <param name="value">trueの場合配信する</param>
		void StreamAction(bool value);
	}

	internal static class IControlerProperty {
		/// <summary>
		/// Adventurer3の画像更新時のプロパティ名
		/// </summary>
		public const string image = "Adventurer3Image";
	}

	/// <summary>
	/// Adventurer3の制御用モデル(Adventurer3のラッパー)
	/// </summary>
	internal class Adventurer3Controler : IControler, IDisposable {
		/// <summary>
		/// 接続先の機器
		/// </summary>
		private Adventurer3 adventurer_;
		/// <summary>
		/// 映像の情報デコーダー
		/// </summary>
		private MjpegDecoder decoder_;

		/// <summary>
		/// Adventurer3へのコマンド送信がバッティングしないようにするための排他制御用
		/// </summary>
		private Mutex mut_ = new Mutex();
		/// <summary>
		/// バックグラウンドで実行する機器のステータスチェック用タスク
		/// </summary>
		Task sync_;

		public event PropertyChangedEventHandler PropertyChanged;

		private void RiseProperty(string name) {
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}

		public Adventurer3Controler() {
			decoder_ = new MjpegDecoder();
			decoder_.FrameReady += FrameReady;
		}

		public void Dispose() {
			decoder_.FrameReady -= FrameReady;
			mut_.Dispose();
		}

		/// <summary>
		/// 映像配信のON/OFF
		/// </summary>
		/// <param name="value">trueの場合配信する</param>
		public void StreamAction(bool value) {
			if (IsConnected && value) {
				decoder_.StopStream();
				var url = new System.Uri(string.Format(Properties.Resources.UriMJepg, ConnectedIp));
				decoder_.ParseStream(url);
				RiseProperty(IControlerProperty.image);
			}
			else if (!value) {
				decoder_.StopStream();		
				RiseProperty(IControlerProperty.image);
			}
		}

		/// <summary>
		/// 接続ボタンが押された時の動作
		/// 未接続の場合は、接続をする。接続中の場合は、接続解除をする。
		/// </summary>
		public bool ClickConnect(IPAddress connectIP) {
			if (adventurer_ != null && adventurer_.IsConnected) {
				// 接続解除をする
				mut_.WaitOne();
				adventurer_.End();
				mut_.ReleaseMutex();
				StreamAction(false);
				adventurer_ = null;
				image_ = null;
				RiseProperty(Adventurer3Property.status);
				RiseProperty(Adventurer3Property.connect);
			}
			else if (connectIP != null) {
				// 接続をする
				adventurer_ = new Adventurer3(connectIP);
				if (adventurer_.Start()) {
					StartUpdate();
					RiseProperty(Adventurer3Property.connect);
				}
				else {
					adventurer_ = null;
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// 機器と接続状態にあるかどうか
		/// </summary>
		/// <returns>trueの場合、接続状態にある</returns>
		public bool IsConnected {
			get {
				if (adventurer_ != null) {
					return adventurer_.IsConnected;
				}
				else {
					return false;
				}
			}
		}

		/// <summary>
		/// ステータス更新用のバックグラウンド処理の開始
		/// </summary>
		private void StartUpdate() {
			const int statusCheckInterval_ = 500;

			if (sync_ != null) {
				if (sync_.IsCanceled || sync_.IsCompleted) {
					sync_.Dispose();
					sync_ = null;
				}
			}
			if (sync_ == null) {
				sync_ = Task.Run(() => {
					for (int loop = 0; IsConnected; loop++) {
						if (mut_.WaitOne(0)) {
							// ロック中の場合、データ送信されていることになるので、今回の更新はパスする。
							switch (loop % 4) {
							case 0:
								adventurer_.UpdateJobStatus();
								RiseProperty(Adventurer3Property.status);
								break;
							case 1:
								adventurer_.UpdateMachneStatus();
								RiseProperty(Adventurer3Property.status);
								break;
							case 2:
								adventurer_.UpdateTempStatus();
								RiseProperty(Adventurer3Property.status);
								break;
							case 3:
								adventurer_.UpdatePosition();
								RiseProperty(Adventurer3Property.position);
								break;
							}
							mut_.ReleaseMutex();
						}
						else {
							RiseProperty(Adventurer3Property.status);
						}
						Thread.Sleep(statusCheckInterval_);
					}
					RiseProperty(Adventurer3Property.connect);
				});
			}
		}

		/// <summary>
		/// Gコードファイルを印刷する
		/// </summary>
		/// <param name="sendfile">Gコードファイル</param>
		public async Task<bool> ExecuteGFile(string sendfile, string targetName = null) {
			if (adventurer_ != null) {
				if (string.IsNullOrEmpty(targetName)) {
					targetName = Path.GetFileNameWithoutExtension(sendfile);
				}
				mut_.WaitOne(); // データの送信時は、一括でロックしておく
				var result = await Task.Run(() => {
					return adventurer_.StartJob(sendfile, targetName);
				});
				mut_.ReleaseMutex();
				if (!result) {
					RiseProperty(Adventurer3Property.status);
					RiseProperty(Adventurer3Property.connect);
				}
				return result;
			}
			return false;
		}

		/// <summary>
		/// ファイル転送を中止する
		/// </summary>
		public void StopJob() {
			mut_.WaitOne();
			adventurer_.StopJob();
			mut_.ReleaseMutex();
		}

		/// <summary>
		/// 機器へのコマンド送信
		/// </summary>
		/// <param name="msg">コマンド</param>
		/// <returns>返信のメッセージ</returns>
		public string Send(string msg) {
			var result = "";
			if (adventurer_ != null) {
				if (mut_.WaitOne(200)) {
					result = adventurer_.Send(msg);
					mut_.ReleaseMutex();
				}
			}
			return result;
		}
		/// <summary>
		/// sendで返ってきた値がOKだったかどうかを調べる
		/// </summary>
		/// <param name="retVal">sendの返却値</param>
		/// <returns>trueの場合OKな返却値</returns>
		public bool IsOK(string retVal) {
			if (adventurer_ != null) {
				return adventurer_.IsOK(retVal);
			}
			return false;
		}

		/// <summary>
		/// Adventurer3のIPをサーチするクラス
		/// ARPテーブルから登録済みIPを検索し、そのIPアドレスのMACアドレス先頭02で始まるものを返す
		/// ARPテーブル検索は、GetIpNetTableを使用する
		/// </summary>
		public List<IPAddress> SearchIP() {
			return Adventurer3.SearchIP();
		}

		#region マシンの状態に関する情報
		public bool LimitX {
			get => adventurer_ == null ? false : adventurer_.LimitX;
		}
		public bool LimitY {
			get => adventurer_ == null ? false : adventurer_.LimitY;
		}
		public bool LimitZ {
			get => adventurer_ == null ? false : adventurer_.LimitZ;
		}
		public MachineStatus Status {
			get {
				if (adventurer_ == null) {
					return MachineStatus.Busy;
				}
				else {
					switch (adventurer_.Status) {
					case Adventurer3.machineStatus.Ready:
						return MachineStatus.Ready;
					case Adventurer3.machineStatus.Building:
						return MachineStatus.Building;
					case Adventurer3.machineStatus.Transfer:
						return MachineStatus.Transfer;
					case Adventurer3.machineStatus.Busy:
					default:
						return MachineStatus.Busy;
					}
				}
			}
		}
		public int CurrentTempBed {
			get => adventurer_ == null ? 0 : adventurer_.CurrentTempBed;
		}
		public int CurrentTempNozel {
			get => adventurer_ == null ? 0 : adventurer_.CurrentTempNozel;
		}
		public int TargetTempBed {
			get => adventurer_ == null ? 0 : adventurer_.TargetTempBed;
			set {
				if (CanJobStart) {
					if (CanJobStart) {
						Send(string.Format("M140 S{0} T0", value));
					}
				}
			}
		}
		public int TargetTempNozel {
			get => adventurer_ == null ? 0 : adventurer_.TargetTempNozel;
			set {
				if (CanJobStart) {
					Send(string.Format("M104 S{0} T0", value));
				}
			}
		}
		public int SdProgress {
			get => adventurer_ == null ? 0 : adventurer_.SdProgress;
		}
		public int SdMax {
			get => adventurer_ == null ? 0 : adventurer_.SdMax;
		}
		public long RemainTransferByte {
			get => adventurer_ == null ? 0 : adventurer_.RemainTransferByte;
		}
		public long TransferByte {
			get => adventurer_ == null ? 0 : adventurer_.TransferByte;
		}
		#endregion
		public bool CanJobStart { get => IsConnected && Status == MachineStatus.Ready; }
		public string ConnectedIp { get => IsConnected ? adventurer_.ConnectedIP : ""; }

		public string BaseFolderName { get; set; }

		private void FrameReady(object sender, FrameReadyEventArgs e) {
			image_ = e.BitmapImage;
			RiseProperty(IControlerProperty.image);
		}

		private BitmapImage image_;

		public BitmapImage Image { get => image_; }

		public void Led(bool value) {
			if (IsConnected) {
				adventurer_.Led(value);
			}
		}

		/// <summary>
		/// XYとFのそれぞれの値のチェック。
		/// チェックしない項目にはf以外0を入れる
		/// </summary>
		bool IsValid(double x, double y, uint f) {
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
				if (IsValid(x, y, f)) {
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
				if (IsValid(x, 0.0, f)) {
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
				if (IsValid(0.0, y, f)) {
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
			adventurer_.UpdatePosition();
			adventurer_.UpdateMachneStatus();
		}

		#region 座標系の情報(Adventurer3Property.positionでの通知対象)
		public double PosX { get => adventurer_ == null ? 0 : adventurer_.PosX; }
		public double PosY { get => adventurer_ == null ? 0 : adventurer_.PosY; }
		public double PosZ { get => adventurer_ == null ? 0 : adventurer_.PosZ; }
		public double PosE { get => adventurer_ == null ? 0 : adventurer_.PosE; }
		#endregion

	}
}
