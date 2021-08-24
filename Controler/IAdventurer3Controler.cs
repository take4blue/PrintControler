using System.ComponentModel;

namespace PrintControler.Controler {
	/// <summary>
	/// マシンの制御状態
	/// </summary>
	public enum MachineStatus {
		/// <summary>
		/// コマンド待機状態
		/// </summary>
		Ready,
		/// <summary>
		/// コマンド実施状態
		/// </summary>
		Busy,
		/// <summary>
		/// 出力状態
		/// </summary>
		Building,
		/// <summary>
		/// データ転送状態
		/// </summary>
		Transfer,
	}

	/// <summary>
	/// プロパティ名
	/// </summary>
	public static class Adventurer3Property {
		/// <summary>
		/// Adventurer3の機器のステータス更新時のプロパティ名
		/// </summary>
		public const string status = "adventurer3Status";
		/// <summary>
		/// Adventurer3の機器の座標更新時のプロパティ名
		/// </summary>
		public const string position = "adventurer3Position";
		/// <summary>
		/// Adventurer3の機器接続ON/OFF更新時のプロパティ名
		/// </summary>
		public const string connect = "adventurer3Connect";
		/// <summary>
		/// パラメータタブのリージョン名
		/// </summary>
		public const string tabRegionName = "ParameterTabRegion";
	}

	/// <summary>
	/// モジュール側から操作可能なAdventurer3制御インターフェース
	/// またマシンの状態に関する情報、adventurer3Statusという名前でプロパティ変更のイベントが発生する。
	/// ステータスの更新は最大で3秒で考えている。
	/// </summary>
	public interface IAdventurer3Controler : INotifyPropertyChanged {
		/// <summary>
		/// 機器と接続状態にあるかどうか
		/// </summary>
		/// <returns>trueの場合、接続状態にある</returns>
		bool IsConnected { get; }
		/// <summary>
		/// JOB中止
		/// </summary>
		void StopJob();
		/// <summary>
		/// 機器へのコマンド送信
		/// ファイル転送中とかなど、タイミングによっては、送信失敗をする可能性がある。
		/// </summary>
		/// <param name="msg">コマンド</param>
		/// <returns>返信のメッセージ、空文字の場合、送信失敗と思っていい</returns>
		string Send(string msg);
		/// <summary>
		/// sendで返ってきた値がOKだったかどうかを調べる
		/// </summary>
		/// <param name="retVal">sendの返却値</param>
		/// <returns>trueの場合OKな返却値</returns>
		bool IsOK(string retVal);
		#region マシンの状態に関する情報(Adventurer3Property.statusでの通知対象)
		/// <summary>
		/// X軸のリミットスイッチが押されているかどうか
		/// </summary>
		bool LimitX { get; }
		/// <summary>
		/// Y軸のリミットスイッチが押されているかどうか
		/// </summary>
		bool LimitY { get; }
		/// <summary>
		/// Z軸のリミットスイッチが押されているかどうか
		/// </summary>
		bool LimitZ { get; }
		/// <summary>
		/// 機器の状態
		/// </summary>
		MachineStatus Status { get; }
		/// <summary>
		/// 現在のベッドの温度
		/// </summary>
		int CurrentTempBed { get; }
		/// <summary>
		/// 現在のノズルの温度
		/// </summary>
		int CurrentTempNozel { get; }
		/// <summary>
		/// 設定されているベッドの温度
		/// データの設定はcanJobStart==trueの場合のみ可能。
		/// それ以外の場合は値は変わらず。
		/// 温度をもとに戻したい場合は0を設定する。
		/// </summary>
		int TargetTempBed { get; set; }
		/// <summary>
		/// 設定されているノズルの温度
		/// データの設定はcanJobStart==trueの場合のみ可能。
		/// それ以外の場合は値は変わらず。
		/// 温度をもとに戻したい場合は0を設定する。
		/// </summary>
		int TargetTempNozel { get; set; }
		/// <summary>
		/// 印刷時の進捗状況(0～100)
		/// </summary>
		int SdProgress { get; }
		/// <summary>
		/// 印刷時の進捗状況の到達点:必ず100になっている
		/// </summary>
		int SdMax { get; }
		#endregion
		#region 座標系の情報(Adventurer3Property.positionでの通知対象)
		/// <summary>
		/// X軸の現在の値
		/// </summary>
		double PosX { get; }
		/// <summary>
		/// Y軸の現在の値
		/// </summary>
		double PosY { get; }
		/// <summary>
		/// Z軸の現在の値
		/// </summary>
		double PosZ { get; }
		/// <summary>
		/// エクストルーダの現在の値
		/// </summary>
		double PosE { get; }
		#endregion
		/// <summary>
		/// プログラムのパラメータを保存してあるフォルダ名
		/// </summary>
		string BaseFolderName { get; set; }
		/// <summary>
		/// 印刷が開始可能かどうか
		/// 基本的には、isConnected && statusがReadyの状態の場合trueになっている
		/// PrintEventを発行する場合、最低限これがtrueになっている必要がある
		/// </summary>
		bool CanJobStart { get; }

		/// <summary>
		/// XY軸の移動(canJobStart==trueの場合のみ動作)
		/// X,Yは移動可能範囲内のみ動作
		/// </summary>
		/// <param name="f">移動スピード(mm/分):1以上の場合動作</param>
		void MoveXY(double x, double y, uint f);
		/// <summary>
		/// X軸の移動(canJobStart==trueの場合のみ動作)
		/// Xは移動可能範囲内のみ動作
		/// </summary>
		/// <param name="f">移動スピード(mm/分):1以上の場合動作</param>
		void MoveX(double x, uint f);
		/// <summary>
		/// Y軸の移動(canJobStart==trueの場合のみ動作)
		/// Yは移動可能範囲内のみ動作
		/// </summary>
		/// <param name="f">移動スピード(mm/分):1以上の場合動作</param>
		void MoveY(double y, uint f);
		/// <summary>
		/// Z軸の移動(canJobStart==trueの場合のみ動作)
		/// Zは移動可能範囲内のみ動作
		/// </summary>
		/// <param name="f">移動スピード(mm/分):1以上の場合動作</param>
		void MoveZ(double z, uint f);
		/// <summary>
		/// エクストルーダーの送り出し(canJobStart==trueの場合のみ動作)
		/// ノズル先端までフィラメントが詰まっていてノズル温度が低い場合、エクストルーダー部からノック音が出る可能性があるので注意
		/// 上記に対するチェックはしていない。
		/// </summary>
		/// <param name="f">送り出しスピード(mm/分):1以上の場合動作</param>
		void MoveE(double e, uint f);
		/// <summary>
		/// 緊急停止(M112を送信する)
		/// </summary>
		void EmergencyStop();
	}

}
