using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace PrintControler.MControl {
	/// <summary>
	/// ノズル移動・フィラメント移動を指示するためのデータクラス
	/// データの検証はこのクラスでは行わない。
	/// 設定データを出すかどうかは、IAdventurer3Controlerのmoveメソッド内で自動判断とする
	/// そのため、目視確認で動かなかったら、値を自分で変更する
	/// データは保存対象とする
	/// </summary>
	[DataContract]
	public class MoveElement : INotifyPropertyChanged {
		/// <summary>
		/// 移動の種類。
		/// XY方向の移動、Z方向の移動、フィラメントの送り出しは異なるG1コードで実施するため、移動の選択制にしている
		/// </summary>
		[DataContract]
		public enum MoveType {
			XYMove = 0,
			XMove = 1,
			YMove = 2,
			ZMove = 3,
			EMove = 4,
			NoMove = 5,
		}
		private MoveType type_ = MoveType.XYMove;
		private double posX_ = 0.0;
		private double posY_ = 0.0;
		private double posZ_ = 0.0;
		private double posE_ = 0.0;

		public event PropertyChangedEventHandler PropertyChanged;

		void RaisePropertyChanged([CallerMemberName] string name = null) {
			if (PropertyChanged == null) {
				return;
			}
			PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
		}
		void SetProperty<T>(ref T target, T value, [CallerMemberName] string name = null) where T:IComparable {
			if (!target.Equals(value)) {
				target = value;
				RaisePropertyChanged(name);
			}
		}

		void TypeChange(MoveType value, bool isSet) {
			if (isSet) {
				if (type_ == MoveType.ZMove || type_ == MoveType.EMove || type_ == MoveType.NoMove
					|| value == MoveType.ZMove || value == MoveType.EMove) {
					Type = value;
				}
				else if (type_ != MoveType.XYMove) {
					if (type_ != value) {
						Type = MoveType.XYMove;
					}
				}
			}
			else {
				if (type_ == value) {
					Type= MoveType.NoMove;
				}
				else if (type_ == MoveType.XYMove) {
					if (value == MoveType.XMove) {
						Type = MoveType.YMove;
					}
					else if (value == MoveType.YMove) {
						Type = MoveType.XMove;
					}
				}
			}
			RaisePropertyChanged(nameof(MoveX));
			RaisePropertyChanged(nameof(MoveY));
			RaisePropertyChanged(nameof(MoveZ));
			RaisePropertyChanged(nameof(MoveE));
		}

		[DataMember(Name ="type")]
		public MoveType Type {
			get => type_;
			set => SetProperty(ref type_, value);
		}
		[DataMember(Name ="X")]
		public double PosX {
			get => posX_;
			set => SetProperty(ref posX_, value);
		}
		[DataMember(Name ="Y")]
		public double PosY {
			get => posY_;
			set => SetProperty(ref posY_, value);
		}
		[DataMember(Name = "Z")]
		public double PosZ {
			get => posZ_;
			set => SetProperty(ref posZ_, value);
		}
		[DataMember(Name = "E")]
		public double PosE {
			get => posE_;
			set => SetProperty(ref posE_, value);
		}

		public bool MoveX {
			get => type_ == MoveType.XYMove || type_ == MoveType.XMove;
			set => TypeChange(MoveType.XMove, value);
		}
		public bool MoveY {
			get => type_ == MoveType.XYMove || type_ == MoveType.YMove;
			set => TypeChange(MoveType.YMove, value);
		}
		public bool MoveZ {
			get => type_ == MoveType.ZMove;
			set => TypeChange(MoveType.ZMove, value);
		}
		public bool MoveE {
			get => type_ == MoveType.EMove;
			set => TypeChange(MoveType.EMove, value);
		}
	}

	/// <summary>
	/// コントロール画面の格納データ
	/// speed系のデフォルト値は、FlashPrintの規定値を流用
	/// XYとEの最大値はFlashPrintから、Zは適当
	/// ベッド・ノズル温度の最大値はFlashPrintから値を流用
	/// </summary>
	[DataContract]
	internal class ControlData {
		ObservableCollection<MoveElement> moveData_ = new ObservableCollection<MoveElement>();
		int targetTempNozel_ = 0;
		int targetTempBed_ = 0;
		int speedXY_ = 3000;
		int speedZ_ = 420;
		int speedE_ = 180;

		[DataMember]
		public ObservableCollection<MoveElement> MoveData {
			get => moveData_;
			set => SetProperty(ref moveData_, value);
		}
		public int TargetTempNozel {
			get => targetTempNozel_;
			set => SetProperty(ref targetTempNozel_, value);
		}
		public int TargetTempBed {
			get => targetTempBed_;
			set => SetProperty(ref targetTempBed_, value);
		}
		[DataMember]
		public int SpeedXY {
			get => speedXY_;
			set => SetProperty(ref speedXY_, value);
		}
		[DataMember]
		public int SpeedZ {
			get => speedZ_;
			set => SetProperty(ref speedZ_, value);
		}
		[DataMember]
		public int SpeedE {
			get => speedE_;
			set => SetProperty(ref speedE_, value);
		}
		
		void SetProperty<T>(ref T target, T source) {
			target = source;
		}

		/// <summary>intの範囲チェック</summary>
		static void Check<T>(T value, T min, T max, ref Dictionary<string, string> msg, string name) where T:IComparable {
			if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0) {
				msg[name] = string.Format(Properties.Resources.MsgOutOfRange, value, min, max);
			}
		}

		/// <summary>
		/// 設定値が正しいかどうかの判断
		/// </summary>
		/// <returns>エラーがあった場合メンバー名+メッセージが出力される</returns>
		public Dictionary<string, string> IsValid() {
			Dictionary<string, string> result = new Dictionary<string, string>();
			Check(SpeedXY, 1, 9000, ref result, nameof(SpeedXY));
			Check(SpeedZ, 1, 3000, ref result, nameof(SpeedZ));
			Check(SpeedE, 1, 6000, ref result, nameof(SpeedE));
			Check(TargetTempBed, 0, 100, ref result, nameof(TargetTempBed));
			Check(TargetTempNozel, 0, 240, ref result, nameof(TargetTempNozel));
			return result;
		}
	}
}
