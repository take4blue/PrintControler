using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Take4.Rs274ngcParser;

namespace Take4.Translator {
	/// <summary>
	/// Adventurer3用へファイルを更新するためのパラメータ
	/// </summary>
	[DataContract]
	public class ToAdventurer3Parameter {
		/// <summary>
		/// ブリムのスピード設定方法
		/// </summary>
		[DataContract]
		public enum BrimSpeedType {
			/// <summary>
			/// 変更しない
			/// </summary>
			NoChange = 0,
			/// <summary>
			/// 絶対値で変更する
			/// </summary>
			Absolute = 1,
			/// <summary>
			/// ファイル中で出力されたデータの割合で変更する
			/// </summary>
			Ratio = 2
		}
		/// <summary>
		/// 筐体ファンの制御
		/// </summary>
		[DataMember]
		public bool EnclosureFanOn { set; get; }
		/// <summary>
		/// モーターXの電圧割合
		/// </summary>
		[DataMember]
		public byte MotorX { set; get; }
		/// <summary>
		/// モーターYの電圧割合
		/// </summary>
		[DataMember]
		public byte MotorY { set; get; }
		/// <summary>
		/// モーターZの電圧割合
		/// </summary>
		[DataMember]
		public byte MotorZ { set; get; }
		/// <summary>
		/// モーターAの電圧割合
		/// </summary>
		[DataMember]
		public byte MotorA { set; get; }
		/// <summary>
		/// モーターBの電圧割合
		/// </summary>
		[DataMember]
		public byte MotorB { set; get; }
		/// <summary>
		/// 遊び除去のための移動距離
		/// </summary>
		[DataMember]
		public double PlayRemovalLength { set; get; }
		/// <summary>
		/// グローバルオフセット量
		/// </summary>
		[DataMember]
		public double OffsetZ { set; get; }
		/// <summary>
		/// ブリムスピードの補正方法
		/// </summary>
		[DataMember]
		public BrimSpeedType BrimSpeedTypeValue { set; get; }
		/// <summary>
		/// 絶対値でのブリム補正値
		/// </summary>
		[DataMember]
		public int BrimSpeed { set; get; }
		/// <summary>
		/// 既存値からの相対値でのブリム補正値
		/// </summary>
		[DataMember]
		public int BrimSpeedRatio { set; get; }
		/// <summary>
		/// ブリムの出力割合
		/// </summary>
		[DataMember]
		public int BrimExtrudeRatio { set; get; }

		public ToAdventurer3Parameter() {
			EnclosureFanOn = false;
			MotorX = MotorY = MotorA = 100;
			MotorZ = 40;
			MotorB = 20;
			PlayRemovalLength = 0.5;
			OffsetZ = 0.0;
			BrimSpeed = 420;
			BrimSpeedRatio = 50;
			BrimExtrudeRatio = 20;
			BrimSpeedTypeValue = BrimSpeedType.NoChange;
		}

		public void Set(ToAdventurer3Parameter source) {
			EnclosureFanOn = source.EnclosureFanOn;
			MotorX = source.MotorX;
			MotorY = source.MotorY;
			MotorZ = source.MotorZ;
			MotorA = source.MotorA;
			MotorB = source.MotorB;
			PlayRemovalLength = source.PlayRemovalLength;
			OffsetZ = source.OffsetZ;
			BrimSpeedTypeValue = source.BrimSpeedTypeValue;
			BrimSpeed = source.BrimSpeed;
			BrimSpeedRatio = source.BrimSpeedRatio;
			BrimExtrudeRatio = source.BrimExtrudeRatio;
		}

		/// <summary>intの範囲チェック</summary>
		static void Check(int value, int min, int max, ref Dictionary<string, string> msg, string name) {
			if (value < min || value > max) {
				msg[name] = string.Format(Properties.Resources.MsgOutOfRange, value, min, max);
			}
		}
		/// <summary>doubleの範囲チェック</summary>
		static void Check(double value, double min, double max, int fp, ref Dictionary<string, string> msg, string name) {
			var work = Math.Round(value, fp, MidpointRounding.AwayFromZero);
			if (work < min || work > max) {
				msg[name] = string.Format(Properties.Resources.MsgOutOfRange, work, min, max);
			}
		}

		/// <summary>
		/// 設定内容の値チェック
		/// </summary>
		/// <param name="isMMS">単位系がmm/sかどうか</param>
		/// <returns>属性名とエラーメッセージのペアを返す</returns>
		public Dictionary<string, string> IsValid(bool isMMS) {
			var ret = new Dictionary<string, string>();
			Check(MotorA, 1, 100, ref ret, nameof(MotorA));
			Check(MotorB, 1, 100, ref ret, nameof(MotorB));
			Check(MotorX, 1, 100, ref ret, nameof(MotorX));
			Check(MotorY, 1, 100, ref ret, nameof(MotorY));
			Check(MotorZ, 1, 100, ref ret, nameof(MotorZ));
			Check(PlayRemovalLength, 0.0, 10.0, 2, ref ret, nameof(PlayRemovalLength));
			Check(OffsetZ, -10.0, 10.0, 2, ref ret, nameof(OffsetZ));
			Check(BrimSpeed / (isMMS ? 60 : 1), 1, 4800 / (isMMS ? 60 : 1), ref ret, nameof(BrimSpeed));
			Check(BrimSpeedRatio, 1, 999, ref ret, nameof(BrimSpeedRatio));
			Check(BrimExtrudeRatio, 1, 999, ref ret, nameof(BrimExtrudeRatio));
			return ret;
		}
	}

	/// <summary>
	/// Simplify3Dで出力されたファイル内のパラメータ。
	/// </summary>
	internal class Simplify3DParameter {
		public int RapidXYSpeed = 4800;
		public int RapidZSpeed = 300;
		public int DefaultSpeed = 3600;

		/// <summary>
		/// パラメータ行の解析
		/// </summary>
		/// <param name="comment">コメントの情報</param>
		/// <returns>パラメータとして落とし込めたらtrue</returns>
		public bool ParseParameter(string comment) {
			if (!string.IsNullOrEmpty(comment)) {
				int result;
				var lines = comment.Trim().Split(',');
				if (lines.Length >= 2) {
					if (lines[0].StartsWith(";   defaultSpeed")) {
						if (int.TryParse(lines[1], out result)) {
							DefaultSpeed = result;
							return true;
						}
					}
					else if (lines[0].StartsWith(";   rapidXYspeed")) {
						if (int.TryParse(lines[1], out result)) {
							RapidXYSpeed = result;
							return true;
						}
					}
					else if (lines[0].StartsWith(";   rapidZspeed")) {
						if (int.TryParse(lines[1], out result)) {
							RapidZSpeed = result;
							return true;
						}
					}
				}
			}
			return false;
		}
	}

	/// <summary>
	/// Adventurer3用にGコードの内容を更新する
	/// </summary>
	public class ToAdventurer3 : ICommandActor {
		/// <summary>
		/// ファイル種別
		/// </summary>
		enum FileType {
			FlashPrint,
			Simplify3D,
			Slic3r,
			Other,
		}
		/// <summary>
		/// 現在処理しているファイルの種別
		/// </summary>
		FileType fileType_ = FileType.Other;

		StreamWriter outputFile_;

		ToAdventurer3Parameter modifyParameter_ = new ToAdventurer3Parameter();
		Simplify3DParameter s3dParameter_ = null;
		ValueXYZEF coordinate_;

		/// <summary>
		/// パラメータの入出力
		/// </summary>
		public ToAdventurer3Parameter Parameter {
			get => modifyParameter_;
			set => modifyParameter_.Set(value);
		}

		/// <summary>
		/// Simplify3D/Slic3r形式で、ファイルの先頭側で出力する項目
		/// </summary>
		void WriteHeader() {
			outputFile_.WriteLine("G28");
			outputFile_.WriteLine("M132 X Y Z A B");
			outputFile_.WriteLine(string.Format("G1 Z50.000 F{0}", s3dParameter_.RapidZSpeed));
			outputFile_.WriteLine(string.Format("G161 X Y F{0}", s3dParameter_.RapidXYSpeed));
			outputFile_.WriteLine("M7 T0");
			outputFile_.WriteLine("M6 T0");
			if (modifyParameter_.EnclosureFanOn) {
				outputFile_.WriteLine("M651");
			}
			outputFile_.WriteLine(string.Format("M907 X{0} Y{1} Z{2} A{3} B{4}", modifyParameter_.MotorX, modifyParameter_.MotorY, modifyParameter_.MotorZ, modifyParameter_.MotorA, modifyParameter_.MotorB));
		}

		/// <summary>
		/// Simplify3D/Slic3r形式で、ファイルの終了側で出力する項目
		/// </summary>
		void WriteFooter() {
			outputFile_.WriteLine("M104 S0 T0");
			outputFile_.WriteLine("M140 S0 T0");
			outputFile_.WriteLine(string.Format("G162 Z F{0}", s3dParameter_.RapidZSpeed));
			outputFile_.WriteLine("M107");
			outputFile_.WriteLine("G28 X Y");
			if (modifyParameter_.EnclosureFanOn) {
				outputFile_.WriteLine("M652");
			}
			outputFile_.WriteLine("M132 X Y Z A B");
			outputFile_.WriteLine("G91");
			outputFile_.WriteLine("M18");
		}

		/// <summary>
		/// 遊び除去用コマンドの挿入
		/// </summary>
		void WritePlayRemoval(int rapidZspeed) {
			if (modifyParameter_.PlayRemovalLength != 0.0) {
				outputFile_.WriteLine("G91");
				outputFile_.WriteLine(string.Format("G1 Z{0:F1} F{1}", modifyParameter_.PlayRemovalLength, rapidZspeed));
				outputFile_.WriteLine("G90");
			}
		}

		/// <summary>
		/// ファイルの種別を調べて、必要であれば、前処理を行う。
		/// </summary>
		/// <returns>trueの場合、処理可能なファイル</returns>
		bool CheckFileType(Stream inputFile) {
			// 1行目相当を読み込み、ファイル種別を取得する
			var buffer = new List<Byte>();
			for (var val = inputFile.ReadByte(); val != -1; val = inputFile.ReadByte()) {
				buffer.Add((Byte)val);
				if (val == 0x0a) {
					break;
				}
			}
			var firstLine = Encoding.ASCII.GetString(buffer.ToArray());

			if (firstLine.StartsWith("xgcode 1.0")) {
				fileType_ = FileType.FlashPrint;
				// ;start gcodeまで出力をしない。
				int headerSize;
				{
					// まずヘッダーに関する情報を取得、出力する。
					var work = new byte[0x20 - firstLine.Length];
					var size = inputFile.Read(work, 0, work.Length);
					headerSize = BitConverter.ToInt32(work, 0x14 - firstLine.Length);
				}
				{
					var work = new byte[headerSize - 0x20];
					var size = inputFile.Read(work, 0, work.Length);
				}
				return true;
			}
			else if (firstLine.StartsWith("; G-Code generated by Simplify3D(R) Version 4.1")) {
				fileType_ = FileType.Simplify3D;
				return true;
			}
			else if (firstLine.StartsWith("; generated by Slic3r take4")) {
				fileType_ = FileType.Slic3r;
				return true;
			}
			return false;
		}

		bool isPreOutputed_ = false;
		bool brimSection_ = false;
		bool doMoveExtrudeOutput_ = false;

		/// <summary>
		/// コメント行の解析
		/// </summary>
		/// <param name="line">1行情報</param>
		void ParseComment(LineCommand line) {
			if (!string.IsNullOrEmpty(line.Comment) && line.Count == 0) {
				switch (fileType_) {
				case FileType.Simplify3D:
					if (!s3dParameter_.ParseParameter(line.Comment)) {
						// 解釈できなものだった場合の処理
						if (line.Comment.StartsWith("; layer end")) {
							// ポスト処理を実施する
							WriteFooter();
						}
						else if (line.Comment.StartsWith("; process") && !isPreOutputed_) {
							// プリ処理を実施する
							WriteHeader();
							isPreOutputed_ = true;
						}
						else if (line.Comment.StartsWith("; feature skirt")) {
							// ブリム開始位置
							brimSection_ = true;
						}
						else if (line.Comment.StartsWith("; feature")) {
							// ブリム終了位置
							brimSection_ = false;
						}
					}
					break;
				case FileType.Slic3r:
					if (!s3dParameter_.ParseParameter(line.Comment)) {
						if (line.Comment.StartsWith("; start gcode")) {
							// プリ処理を実施する
							WriteHeader();
							isPreOutputed_ = true;
						}
						else if (line.Comment.StartsWith(";END gcode for filament")) {
							// ポスト処理を実施する
							WriteFooter();
						}
					}
					break;
				default:
					outputFile_.WriteLine(OutputGCode.ToString(line));
					break;
				}
			}
		}

		public bool ActionLine(LineCommand line) {
			ParseComment(line);
			Double prevZ = coordinate_.Z;

			if (coordinate_.ActionLine(line)) {
				Double gcode;
				int position;
				if (line.TryGetValue('G', out gcode, out position) && gcode == 1) {
					// 以下G1コードのみ実行する
					var hasE = line.Has('E');
					var hasX = line.Has('X');
					var hasY = line.Has('Y');
					var hasZ = line.Has('Z');

					if (hasX && hasY && hasE) {
						// 造形のための射出開始
						doMoveExtrudeOutput_ = true;
					}

					if (!doMoveExtrudeOutput_ && hasE && !hasX && !hasY && !hasZ) {
						// 造形のための射出をする前のEの変更動作は出力しないようにする
						return true;
					}
					else if (hasZ) {
						// Z軸移動に関する処理を行う
						if (coordinate_.IsAbsolute && modifyParameter_.OffsetZ != 0.0) {
							// グローバルオフセットでZ値を更新
							line.Modify('Z', (x) => x.Value += modifyParameter_.OffsetZ);
						}
						if (coordinate_.Z > prevZ) {
							// Z値が、前の値より上に行った場合、遊び除去を実施
							WritePlayRemoval(fileType_ == FileType.FlashPrint ? (int)coordinate_.F : s3dParameter_.RapidZSpeed);
						}
					}
					else if (brimSection_) {
						if (hasE && hasX && hasY) {
							if (modifyParameter_.BrimExtrudeRatio != 100) {
								// ブリム吐出量の修正を行う
								line.Modify('E', (x) => x.Value *= (double)modifyParameter_.BrimExtrudeRatio / 100.0);
							}
							if (hasX && hasY && line.Has('F') && modifyParameter_.BrimSpeedTypeValue != ToAdventurer3Parameter.BrimSpeedType.NoChange) {
								// ブリムの吐出速度を変更
								Double after = modifyParameter_.BrimSpeedTypeValue == ToAdventurer3Parameter.BrimSpeedType.Absolute ? modifyParameter_.BrimSpeed :
									coordinate_.F * (double)modifyParameter_.BrimSpeedRatio / 100.0;
								line.Modify('F', (x) => x.Value = after);
							}
						}
					}
				}
			}
			if (line.Count != 0) {
				// コメント行のみの場合は出力しない
				outputFile_.WriteLine(OutputGCode.ToString(line));
			}
			return true;
		}

		public void PostAction() {
			outputFile_.Flush();
		}

		public void PreAction() {
			// 何もしない
		}

		/// <summary>
		/// ファイルの更新処理
		/// </summary>
		/// <param name="inputFile">入力ファイル</param>
		/// <param name="outputFile">出力ファイル</param>
		/// <returns>更新が正しく終了した場合true</returns>
		public bool Modify(Stream inputFile, Stream outputFile) {
			outputFile_ = new StreamWriter(outputFile, Encoding.ASCII, 4096, true);
			inputFile.Seek(0, SeekOrigin.Begin);
			if (CheckFileType(inputFile)) {
				if (fileType_ == FileType.FlashPrint) {
					outputFile_.NewLine = "\n";
				}
				var modifier = new ParseGCodeStream();
				coordinate_ = new ValueXYZEF();
				s3dParameter_ = new Simplify3DParameter();
				brimSection_ = false;
				coordinate_.Z = 10000.0;  // 遊び除去コードの実施のため大きな値をとりあえず設定しておく
				isPreOutputed_ = false;
				doMoveExtrudeOutput_ = false;
				return modifier.Parse(inputFile, this);
			}
			else {
				// 処理対象外のファイル種別
				return false;
			}
		}
	}
}
