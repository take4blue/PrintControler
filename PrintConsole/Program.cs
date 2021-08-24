using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Take4.AControler;
using Take4.Translator;
using System.Runtime.Serialization.Json;

namespace PrintConsole {
	class Program {
		static void Main(string[] args) {
			if (args.Length > 0) {
				var work = new Program();
				if (work.connect(args[0])) {
					work.printInfomation();
					work.startUpdate();
					work.ui();
				}
			}
			else if (args.Length > 1) {
				if (File.Exists(args[0]) && File.Exists(args[1]) && args[1].EndsWith(".json")) {
					var modifier = new ToAdventurer3();
					using (var wStream = File.OpenRead(args[1])) {
						var settings = new DataContractJsonSerializerSettings();
						var serializer = new DataContractJsonSerializer(typeof(ToAdventurer3Parameter), settings);
						var data1 = (ToAdventurer3Parameter)serializer.ReadObject(wStream);
						modifier.Parameter.Set(data1);
					}
					bool fileModifyResult = false;
					var tempFilename = Path.ChangeExtension(args[0], "test");
					using (var inFile = File.OpenRead(args[0])) {
						using (var outFile = File.Create(tempFilename)) {
							fileModifyResult = modifier.Modify(inFile, outFile);
						}
					}
				}
			}
			else {
				var work = new Program();
				if (work.connect()) {
					work.printInfomation();
					work.startUpdate();
					work.ui();
				}
			}
		}

		Task sync_;
		const int statusCheckInterval_ = 3000;
		private Take4.AControler.Adventurer3 target_;
		/// <summary>
		/// Adventurer3へのコマンド送信がバッティングしないようにするための排他制御用
		/// </summary>
		private Mutex mut_ = new Mutex();

		/// <summary>
		/// Adventurer3との接続
		/// </summary>
		/// <returns>接続できた</returns>
		bool connect() {
			var list = Take4.AControler.Adventurer3.SearchIP();
			if (list.Count > 0) {
				target_ = new Adventurer3(list[0]);
				target_.Start();
				return target_.IsConnected;
			}
			else {
				return false;
			}
		}

		/// <summary>
		/// Adventurer3とのIP指定での接続
		/// </summary>
		/// <param name="ip"></param>
		/// <returns></returns>
		bool connect(string ip)
        {
			System.Net.IPAddress address;
			if (System.Net.IPAddress.TryParse(ip, out address)) {
				target_ = new Adventurer3(address);
				target_.Start();
				return target_.IsConnected;
			}
			return false;
        }

		void printInfomation() {
			var ret = target_.Send("M115");
			if (target_.IsOK(ret)) {
				Console.WriteLine(string.Format("Connected {0}", target_.ConnectedIP));
				Console.WriteLine(ret);
			}
			else {
				Console.WriteLine("Error");
			}
		}

		/// <summary>
		/// ステータス更新用のバックグラウンド処理の開始
		/// </summary>
		void startUpdate() {
			if (sync_ != null) {
				if (sync_.IsCanceled || sync_.IsCompleted) {
					sync_.Dispose();
					sync_ = null;
				}
			}
			if (sync_ == null) {
				sync_ = Task.Run(() => statusUpdate());
			}
		}

		/// <summary>
		/// 1秒単位にステータス更新
		/// </summary>
		void statusUpdate() {
			while(target_.IsConnected) {
				if (mut_.WaitOne(0)) {
					// ロック中の場合、データ送信されていることになるので、今回の更新はパスする。
					target_.UpdateStatus();
					mut_.ReleaseMutex();
				}
				Thread.Sleep(statusCheckInterval_);
			}
		}

		/// <summary>
		/// 印刷コマンド
		/// </summary>
		/// <param name="command">コマンド名そのまま</param>
		void print(string command) {
			// コマンドをとりあえずスペースでパースする
			var cmds = command.Split(' ');
			var modifier = new ToAdventurer3();
			var filename = cmds[0].Substring(1);
			if (cmds.Length >= 2 && cmds[1].EndsWith(".json")) {
				if (File.Exists(cmds[1])) {
					using (var wStream = File.OpenRead(cmds[1])) {
						var settings = new DataContractJsonSerializerSettings();
						var serializer = new DataContractJsonSerializer(typeof(ToAdventurer3Parameter), settings);
						var data1 = (ToAdventurer3Parameter)serializer.ReadObject(wStream);
						modifier.Parameter.Set(data1);
					}
					bool fileModifyResult = false;
					var tempFilename = Path.GetTempFileName();
					using (var inFile = File.OpenRead(filename)) {
						using (var outFile = File.Create(tempFilename)) {
							fileModifyResult = modifier.Modify(inFile, outFile);
						}
					}
					if (fileModifyResult) {
						if (mut_.WaitOne()) {
							var ret = target_.StartJob(tempFilename, Path.GetFileNameWithoutExtension(filename));
							mut_.ReleaseMutex();
							if (!ret) {
								Console.WriteLine("Error");
							}
							else {
								Console.WriteLine("Start Job");
							}
						}
						File.Delete(tempFilename);
					}
				}
			}
			else {
				if (mut_.WaitOne()) {
					var ret = target_.StartJob(filename);
					mut_.ReleaseMutex();
					if (!ret) {
						Console.WriteLine("Error");
					}
					else {
						Console.WriteLine("Start Job");
					}
				}
			}
		}

		/// <summary>
		/// UI操作
		/// </summary>
		void ui() {
			for (; ;) {
				Console.Write("> ");
				var cmd = Console.ReadLine();
				if (!target_.IsConnected) {
					target_.Start();
					if (!target_.IsConnected) {
						// 再接続できなかったので終了
						break;
					}
					startUpdate();
				}
				// コマンド処理
				cmd = cmd.Trim();
				if (cmd.StartsWith("q") || cmd.StartsWith("Q")) {
					// コマンド中止
					target_.End();
					break;
				}
				if (cmd.StartsWith("p") || cmd.StartsWith("P")) {
					Console.WriteLine(target_.GetStatus());
				}
				else if (cmd.StartsWith("l") || cmd.StartsWith("L")) {
					if (mut_.WaitOne()) {
						if (cmd.EndsWith("0")) {
							target_.Led(false);
						}
						else {
							target_.Led(true);
						}
						mut_.ReleaseMutex();
					}
				}
				else if (cmd.StartsWith("s") || cmd.StartsWith("S")) {
					if (mut_.WaitOne()) {
						target_.Stop();
						mut_.ReleaseMutex();
						Console.WriteLine("Stop");
					}
				}
				else if (cmd.StartsWith("G") || cmd.StartsWith("M")) {
					if (mut_.WaitOne()) {
						var ret = target_.Send(cmd);
						if (ret != null) {
							Console.WriteLine(ret);
						}
						else {
							// データ送受信失敗なので、再接続をする
							Console.WriteLine("Error");
							target_.End();
							target_.Start();
							startUpdate();
						}
						mut_.ReleaseMutex();
					}
				}
				else if (cmd.StartsWith("jobstop")) {
					if (mut_.WaitOne()) {
						target_.StopJob();
						mut_.ReleaseMutex();
						Console.WriteLine("Stop Job");
					}
				}
				else if (cmd.StartsWith("j") || cmd.StartsWith("J")) {
					print(cmd);
				}
			}
		}

		~Program() {
			mut_.Dispose();
		}
	}
}
