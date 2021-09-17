using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;

namespace Take4.AControler {
	/// <summary>
	/// ファイル転送のためのインターフェースクラス
	/// もともとAdventurer3にあったものをこちらに持ってくるので、Adventurer3内のものをアクセスできるように一部メンバはinternalにしておいてある
	/// </summary>
	interface ISender
    {
		/// <summary>
		/// データ送信処理
		/// </summary>
		/// <param name="fp">送信ファイル</param>
		/// <param name="adv">Adventurer3制御オブジェクト</param>
		/// <returns>true : 送信完了</returns>
		bool SendFileData(FileStream fp, Adventurer3 adv);

		/// <summary>
		/// 送信キャンセルの処理
		/// 一応UIから見て非同期処理内で呼び出されるもの
		/// </summary>
		/// <param name="adv">Adventurer3制御オブジェクト</param>
		void SendCancelAction(Adventurer3 adv);

		/// <summary>
		/// 送信情報の初期化
		/// </summary>
		/// <param name="client">初期化対象</param>
		void Initialize(TcpClient client);
	}

	/// <summary>
	/// V1.6でのファイル転送処理
	/// </summary>
	class V1Sender : ISender
    {
		const int blockSize_ = 4096;
		const int headerSize_ = 16;

		static void GetByte(int value, byte[] buffer, uint index)
		{
			buffer[index] = (byte)((value >> 24) & 0xff);
			buffer[index + 1] = (byte)((value >> 16) & 0xff);
			buffer[index + 2] = (byte)((value >> 8) & 0xff);
			buffer[index + 3] = (byte)(value & 0xff);
		}

		/// <summary>
		/// データ送信処理
		/// </summary>
		/// <param name="fp">送信ファイル</param>
		/// <param name="adv">Adventurer3制御オブジェクト</param>
		/// <returns>true : 送信完了</returns>
		/// <remarks>
		/// Sleep100を入れた理由は、機器側からのブロック送信が完了した通信が再送されたため、送受信データの段ずれが起こった。
		/// プロトコルアナライザの結果を見る限り、以下のような送受信だった。
		/// ・PCから0ブロック目送信
		/// ・機器から0ブロックOK受信
		/// ・PCから1ブロック目送信
		/// ・機器から0ブロックOK受信　← これがおかしい
		/// ・PCから2ブロック目送信
		/// PC側からのSEQ/ACKの番号は問題ないようだったのだが、何らかの理由で、PCからの1ブロック目送信のSEQ/ACKが認識できなかったのかも。
		/// ただ、送信されたファイルで正しく出力はされたみたいだった。
		/// ここで、sleepを入れたところ、とりあえず、送受信の段ずれは解消されたが、機器からの0ブロック目OK受信の前に、なにやら機器側から意味不明な送信が来ている。これは未解消。
		/// </remarks>
		public bool SendFileData(FileStream fp, Adventurer3 adv)
        {
			int counter = 0;
			adv.isStop_ = false;
			adv.RemainTransferByte = fp.Length;
			adv.TransferByte = adv.RemainTransferByte;
			var crc32 = new Crc32b();   // 検査用ハッシュ関数
			for (long i = 0; i < fp.Length && !adv.isStop_; i += blockSize_, adv.RemainTransferByte -= blockSize_, counter++) {
				var readBuffer = new byte[blockSize_];
				var readByte = fp.Read(readBuffer, 0, blockSize_);
				if (readByte <= 0) {
					// 読み取りデータがないので、中止
					return false;
				}
				Array.Resize(ref readBuffer, readByte);
				var writeBuffer = new byte[headerSize_ + blockSize_];
				writeBuffer[0] = writeBuffer[1] = 0x5a;
				writeBuffer[2] = writeBuffer[3] = 0xa5;
				GetByte(counter, writeBuffer, 4);
				GetByte(readByte, writeBuffer, 8);
				Array.Copy(crc32.ComputeHash(readBuffer), 0, writeBuffer, 12, 4);
				Array.Copy(readBuffer, 0, writeBuffer, 16, readBuffer.Length);
				var ret = adv.Send(writeBuffer);
				if (!adv.IsOK(ret)) {
					// データの送信ができなかったので中止
					return false;
				}
				if (i == 0) {
					// 初めだけ
					Thread.Sleep(100);
				}
			}
			return true;
		}

		/// <summary>
		/// 送信キャンセルの処理
		/// 一応UIから見て非同期処理内で呼び出されるもの
		/// </summary>
		/// <param name="adv">Adventurer3制御オブジェクト</param>
		public void SendCancelAction(Adventurer3 adv)
        {

        }

		/// <summary>
		/// 送信情報の初期化
		/// </summary>
		/// <param name="client">初期化対象</param>
		public void Initialize(TcpClient client)
        {
			client.SendBufferSize = blockSize_ + headerSize_;
		}
	}

	/// <summary>
	/// V2.1形式のデータ転送方法
	/// </summary>
	class V2Sender : ISender
    {
		const int blockSize_ = 4096;

		/// <summary>
		/// データ送信処理
		/// </summary>
		/// <param name="fp">送信ファイル</param>
		/// <param name="adv">Adventurer3制御オブジェクト</param>
		/// <returns>true : 送信完了</returns>
		public bool SendFileData(FileStream fp, Adventurer3 adv)
		{
			int counter = 0;
			adv.isStop_ = false;
			adv.RemainTransferByte = fp.Length;
			adv.TransferByte = adv.RemainTransferByte;
			for (long i = 0; i < fp.Length && !adv.isStop_; i += blockSize_, adv.RemainTransferByte -= blockSize_, counter++) {
				var readBuffer = new byte[blockSize_];
				var readByte = fp.Read(readBuffer, 0, blockSize_);
				if (readByte <= 0) {
					// 読み取りデータがないので、中止
					return false;
				}
				if (!adv.SendOnly(readBuffer, readByte)) {
					return false;
				}
			}
			Thread.Sleep(100);
			return true;
		}

		/// <summary>
		/// 送信キャンセルの処理
		/// 一応UIから見て非同期処理内で呼び出されるもの
		/// </summary>
		/// <param name="adv">Adventurer3制御オブジェクト</param>
		public void SendCancelAction(Adventurer3 adv)
		{
			// 転送ダイアログがタイムアウトで閉じるまで待つ
			Thread.Sleep(15000);
		}

		/// <summary>
		/// 送信情報の初期化
		/// </summary>
		/// <param name="client">初期化対象</param>
		public void Initialize(TcpClient client)
		{
			client.SendBufferSize = blockSize_;
		}
	}

	/// <summary>
	/// Adventurer3との通信用制御クラス
	/// </summary>
	public class Adventurer3
    {
		/// <summary>
		/// Adventurer3のIPをサーチするクラス
		/// UDPのブロードキャストで検索する
		/// </summary>
		public static List<IPAddress> SearchIP() {
			var ips = new List<IPAddress>();
			byte[] sendBytes = { 0xc0, 0xa8, 0x0b, 0x03, 0x46, 0x51, 0x00, 0x00 };

			// IP一覧取り出し
			// ネットワークアダプタ一覧からIPv4のホストアドレスを検索
			// 見つかったIPアドレスの18001ポートから225.0.0.9の19000ポートにブロードキャストを行いその返却値から機器のIPを求める。
			var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
			foreach (var adapter in interfaces) {
				if (adapter.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up) {
					var properties = adapter.GetIPProperties();

					foreach (var unicast in properties.UnicastAddresses) {
						switch (unicast.Address.AddressFamily) {
						case AddressFamily.InterNetwork:
							if (unicast.IsDnsEligible) {
								var ip = unicast.Address;
								var localPort = new IPEndPoint(ip, 18001);
								var udp = new UdpClient(localPort);
								var targetPort = new IPEndPoint(IPAddress.Parse("225.0.0.9"), 19000);
								udp.EnableBroadcast = true;
								udp.Send(sendBytes, sendBytes.Length, targetPort);
								udp.Client.ReceiveTimeout = 1000;   // ms:タイムアウトの設定

								for (; ; ) {
									try {
										IPEndPoint e = new IPEndPoint(IPAddress.Any, 19000);
										var receiveBytes = udp.Receive(ref e);
										ips.Add(e.Address);
									}
									catch (SocketException) {
										// タイムアウト
										break;
									}
								}
								//UdpClientを閉じる
								udp.Close();
							}
							break;
						}
					}
				}
			}
			return ips;
		}

		/// <summary>
		/// Adventurer3への接続ポート番号(一応固定)
		/// </summary>
		const int Adventurer3Port = 8899;
		string ip_;
		System.Net.Sockets.TcpClient tcp_;

		/// <summary>
		/// データ転送のための送信オブジェクト
		/// 初期値はV1形式
		/// </summary>
		private ISender sender_ = new V1Sender();

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="ip">接続先機器のIPアドレス</param>
		public Adventurer3(IPAddress ip) {
			ip_ = ip.ToString();
		}

		/// <summary>
		/// 接続先IP情報の取り出し
		/// </summary>
		public string ConnectedIP {
			get { return ip_; }
		}

		/// <summary>
		/// データ受信処理
		/// </summary>
		/// <param name="ns">TCPストリーム</param>
		/// <returns>受信文字列</returns>
		private string Receive(NetworkStream ns) {
			System.Text.Encoding enc = System.Text.Encoding.UTF8;
			// サーバーから送られたデータを受信する
			string ret = "";
			var resBytes = new byte[256];
			int resSize = 0;
			do {
				resSize = ns.Read(resBytes, 0, resBytes.Length);
				if (resSize == 0) {
					break;
				}
				string work = new string(enc.GetChars(resBytes), 0, resSize);
				ret += work;
			} while (ns.DataAvailable);
			return ret;
		}

		/// <summary>
		/// 機器と接続状態かどうかの判断
		/// </summary>
		public bool IsConnected {
			get {
				if (tcp_ == null) {
					return false;
				}
				else if (tcp_.Connected) {
					return true;
				}
				else {
					tcp_.Close();
					tcp_ = null;
					return false;
				}
			}
		}

		/// <summary>
		/// 接続開始処理
		/// </summary>
		/// <returns>開始できた</returns>
		public bool Start() {
			if (tcp_ == null) {
				try {
					tcp_ = new TcpClient(ip_, Adventurer3Port);
				}
				catch(SocketException) {
					// ソケット接続エラーの場合
					return false;
				}
				tcp_.ReceiveTimeout = 5000;
				var ns = tcp_.GetStream();
				System.Text.Encoding enc = System.Text.Encoding.UTF8;
				byte[] sendBytes = enc.GetBytes("~M601 S1\r\n");
				//データを送信する
				ns.Write(sendBytes, 0, sendBytes.Length);
				var ret = Receive(ns);
				if (ret.IndexOf("failed") == -1) {
					// 受信文字列を解析してファイル送信方法を決定する
					if (ret.IndexOf("V2.1") == -1) {
						sender_ = new V1Sender();
					}
					else {
						sender_ = new V2Sender();
					}
					sender_.Initialize(tcp_);
				}
				else {
					// M601でfailedが返ってきた場合END処理を行っておく
					End();
					return false;
                }
			}
			return true;
		}

		/// <summary>
		/// 接続終了処理
		/// </summary>
		public void End() {
			if (tcp_ != null) {
				var ns = tcp_.GetStream();
				System.Text.Encoding enc = System.Text.Encoding.UTF8;
				byte[] sendBytes = enc.GetBytes("~M602\r\n");
				//データを送信する
				ns.Write(sendBytes, 0, sendBytes.Length);
				Receive(ns);
				tcp_.Close();
				tcp_ = null;
			}
		}

		/// <summary>
		/// コマンド送信処理
		/// </summary>
		/// <param name="cmd">コマンド文字列</param>
		/// <param name="preOut">コマンドの前に~をつけるかつけないか</param>
		/// <returns>コマンド送信後の受信文字列</returns>
		public string Send(string cmd, bool preOut = true) {
			System.Text.Encoding enc = System.Text.Encoding.UTF8;
			byte[] sendBytes = enc.GetBytes(string.Format("{1}{0}\r\n", cmd, preOut ? "~" : ""));
			return Send(sendBytes);
		}

		/// <summary>
		/// データ送信処理
		/// </summary>
		/// <param name="data">送信データ</param>
		/// <returns>受信文字列</returns>
		public string Send(byte[] data) {
			if (tcp_ != null) {
				var ns = tcp_.GetStream();
				//データを送信する
				try {
					ns.Write(data, 0, data.Length);
					return Receive(ns);
				}
				catch (IOException) {
					// IO例外があった場合、とりあえずtcpを閉じてしまう。
					tcp_ = null;
					return null;
				}
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// コマンド送信のみ
		/// </summary>
		/// <param name="cmd">コマンド文字列</param>
		/// <param name="preOut">コマンドの前に~をつけるかつけないか</param>
		/// <returns>false : 送信失敗</returns>
		public bool SendOnly(string cmd, bool preOut = true)
		{
			System.Text.Encoding enc = System.Text.Encoding.UTF8;
			byte[] sendBytes = enc.GetBytes(string.Format("{1}{0}\r\n", cmd, preOut ? "~" : ""));
			return SendOnly(sendBytes, sendBytes.Length);
		}

		/// <summary>
		/// データ送信のみ
		/// </summary>
		/// <param name="data">送信データ</param>
		/// <returns>false : 送信失敗</returns>
		public bool SendOnly(byte[] data, int Length)
        {
			if (tcp_ != null) {
				var ns = tcp_.GetStream();
				//データを送信する
				try {
					ns.Write(data, 0, Length);
					return true;
				}
				catch (IOException) {
					// IO例外があった場合、とりあえずtcpを閉じてしまう。
					tcp_ = null;
					return false;
				}
			}
			else {
				return false;
			}
		}


		/// <summary>
		/// sendで返ってきた値がOKだったかどうかを調べる
		/// </summary>
		/// <param name="retVal">sendの返却値</param>
		/// <returns>trueの場合OKな返却値</returns>
		public bool IsOK(string retVal) {
			if (retVal != null) {
				if (retVal.Trim().EndsWith("ok")) {
					return true;
				}
				else if (retVal.Trim().EndsWith("ok.")) {
					return true;
				}
			}
			return false;
		}

		#region マシンの状態に関する情報
		public bool LimitX {
			get;
			private set;
		}
		public bool LimitY {
			get;
			private set;
		}
		public bool LimitZ {
			get;
			private set;
		}
		public enum machineStatus {
			Ready,
			Busy,
			Building,
			Transfer,
		}
		public machineStatus Status { get; private set; }
		public int CurrentTempBed { get; private set; }
		public int CurrentTempNozel { get; private set; }
		public int TargetTempBed { get; private set; }
		public int TargetTempNozel { get; private set; }
		public int SdProgress { get; private set; }
		public int SdMax { get; private set; }
		public double PosX { get; private set; }
		public double PosY { get; private set; }
		public double PosZ { get; private set; }
		public double PosE { get; private set; }
		public string GetStatus() {
			return string.Format(Properties.Resources.MsgStatusFormat,
				CurrentTempNozel,
				TargetTempNozel,
				CurrentTempBed,
				TargetTempBed,
				Status.ToString(),
				SdProgress,
				SdMax,
				PosX,
				PosY,
				PosZ,
				PosE);
		}
		#endregion
		/// <summary>
		/// 機器の状態取得
		/// </summary>
		public void UpdateMachneStatus() {
			var work = Send("M119");
			if (IsOK(work)) {
				var split = work.Split('\n');
				foreach (var line in split) {
					var trimLine = line.Trim();
					if (trimLine.StartsWith("Endstop")) {
						var endstop = trimLine.Split(' ');
						if (endstop.Length == 4) {
							LimitX = LimitY = LimitZ = true;
							if (endstop[1].EndsWith("0")) {
								LimitX = false;
							}
							if (endstop[2].EndsWith("0")) {
								LimitY = false;
							}
							if (endstop[3].EndsWith("0")) {
								LimitZ = false;
							}
						}
					}
					else if (trimLine.StartsWith("MachineStatus")) {
						if (trimLine.EndsWith("READY")) {
							Status = machineStatus.Ready;
						}
						else if (trimLine.EndsWith("BUILDING_FROM_SD")) {
							Status = machineStatus.Building;
						}
						else {
							Status = machineStatus.Busy;
						}
					}
				}
			}
		}
		/// <summary>
		/// 温度の情報を更新
		/// </summary>
		public void UpdateTempStatus() {
			var work = Send("M105");
			if (IsOK(work)) {
				var split = work.Split('\n');
				if (split.Length >= 3) {
					char[] delimitter = { ':', '/', 'B' };
					var splitLine = split[1].Trim().Split(delimitter);
					if (splitLine.Length == 6) {
						int temp;
						int.TryParse(splitLine[1], out temp);
						CurrentTempNozel = temp;
						int.TryParse(splitLine[2], out temp);
						TargetTempNozel = temp;
						int.TryParse(splitLine[4], out temp);
						CurrentTempBed = temp;
						int.TryParse(splitLine[5], out temp);
						TargetTempBed = temp;
					}
				}
			}
		}
		/// <summary>
		/// JOB状態を更新
		/// </summary>
		public void UpdateJobStatus() {
			var work = Send("M27");
			if (IsOK(work)) {
				var split = work.Split('\n');
				if (split.Length >= 3) {
					char[] delimitter = { ' ', '/' };
					var splitLine = split[1].Trim().Split(delimitter);
					if (splitLine.Length == 5) {
						int num;
						int.TryParse(splitLine[3], out num);
						SdProgress = num;
						int.TryParse(splitLine[4], out num);
						SdMax = num;
					}
				}
			}
		}
		/// <summary>
		/// JOB状態を更新
		/// </summary>
		public void UpdatePosition() {
			var work = Send("M114");
			if (IsOK(work)) {
				var split = work.Split('\n');
				if (split.Length >= 3) {
					char[] delimitter = { ' ', ':' };
					var splitLine = split[1].Trim().Split(delimitter);
					if (splitLine.Length == 10) {
						double num;
						double.TryParse(splitLine[1], out num);
						PosX = num;
						double.TryParse(splitLine[3], out num);
						PosY = num;
						double.TryParse(splitLine[5], out num);
						PosZ = num;
						double.TryParse(splitLine[7], out num);
						PosE = num;
					}
				}
			}
		}
		/// <summary>
		/// Adventurer3の情報の取り出し(更新)
		/// 機器の状態・温度・JOB状態を取り出す
		/// </summary>
		public void UpdateStatus() {
			// 機器の状態取得
			UpdateMachneStatus();
			// 温度の取得
			UpdateTempStatus();
			// JOB状態の取得
			UpdateJobStatus();
			// 座標の状態取得
			UpdatePosition();
		}

		/// <summary>
		/// LEDの表示・消去
		/// </summary>
		/// <param name="on">LEDの状態</param>
		public void Led(bool on) {
			if (on) {
				Send("M146 r255 g255 b255 F0");
			}
			else {
				Send("M146 r0 g0 b0 F0");
			}
		}

		internal bool isStop_ = false;
		/// <summary>
		/// 残りの転送データ量
		/// </summary>
		public long RemainTransferByte { get; internal set; }
		public long TransferByte { get; internal set; }
		/// <summary>
		/// 転送のストップ
		/// </summary>
		public void StopTransfer() {
			isStop_ = true;
		}

		/// <summary>
		/// JOB開始
		/// </summary>
		/// <param name="filename">出力するファイル</param>
		/// <param name="jobName">
		/// Adventurer3側に表示するファイル名
		/// これを一応JOB名としておく
		/// 基本的には元のファイル名が望ましい
		/// </param>
		public bool StartJob(string filename, string jobName = null) {
			if (jobName == null) {
				// job名が設定されていない場合は、filenameから引用する
				jobName = Path.GetFileName(filename);
			}
			var backup = Status;
			try {
				Status = machineStatus.Transfer;
				using (var fp = new FileStream(filename, FileMode.Open, FileAccess.Read)) {
					if (IsOK(Send("M28 " + fp.Length.ToString() + " 0:/user/" + jobName))) {
						if (!sender_.SendFileData(fp, this)) {
							return false;
                        }
					}
					else {
						return false;
					}
				}
				if (!isStop_) {
					isStop_ = false;
					if (IsOK(Send("M29"))) {
						if (IsOK(Send("M23 " + "0:/user/" + jobName))) {
							return true;
						}
					}
				}
				else {
					SendOnly("M29");    // とりあえずM29を送っておく
					// FlashPrintを見る限り、ここで、いったん接続が切れる。
					// V1の場合、コンソール画面でダイアログが出ているのでその対応が必要
					// そのため一度End()を呼ぶのが良いことにななる
					// V2の場合送信失敗のタイムアウトになるまでダイアログが出続け、機器から応答データ受付ができなくなる。
					// そのためタイムアウトが終わるまで待つのが正解になる
					// こちらが勝手にEndをすると、相手からリターンが来ないので例外が出る、またEndをしないと接続していると解釈され次の処理が面倒になる
					sender_.SendCancelAction(this);
				}
				return false;
			}
			finally {
				Status = backup;
				RemainTransferByte = 0;
				TransferByte = 0;
			}
		}

		/// <summary>
		/// JOB中止
		/// </summary>
		public void StopJob() {
			if (Status == machineStatus.Transfer) {
				StopTransfer();
			}
			else {
				Send("M26");
			}
		}

		/// <summary>
		/// 機器の緊急停止
		/// </summary>
		public void Stop() {
			if (Status == machineStatus.Transfer) {
				StopTransfer();
			}
			else {
				Send("M112");
				UpdatePosition();
			}
		}
	}
}
