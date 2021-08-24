using PrintControler.Controler;
using PrintControler.Views;
using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;

namespace PrintControler.ViewModels {
	internal class ShellViewModel : BindableBase , IDisposable {
		private IControler model_;
		private IApplicationCommands cmds_;

		#region IPアドレスに関する情報
		/// IPアドレスに関する情報は、ViewModel側で管理する。
		/// <summary>
		/// Adventurer3のIPアドレスだと思われるもの一覧
		/// </summary>
		public List<IPAddress> AddressList { set; get; }

		/// <summary>
		/// 接続対象とするIPアドレス
		/// </summary>
		private IPAddress connectIP_;

		/// <summary>
		/// 接続先IP用バインディング先
		/// </summary>
		public string TargetIP {
			set {
				IPAddress work;
				connectIP_ = IPAddress.TryParse(value, out work) ? work : null;
				RaisePropertyChanged();
				RaisePropertyChanged(nameof(HasTarget));
			}
			get => connectIP_ != null ? connectIP_.ToString() : "";
		}
		private bool HasTarget {
			get => TargetIP.Length > 0;
		}
		#endregion

		#region コマンド類
		public DelegateCommand ConnectAction { get; private set; }
		private DelegateCommand<Object> CloseCmd { get; set; }
		#endregion

		/// <summary>
		/// コンストラクタ
		/// </summary>
		public ShellViewModel(IControler model, IApplicationCommands cmds, IRegionManager rm) {
			model_ = model;
			cmds_ = cmds;
			ConnectAction = new DelegateCommand(() => ClickConnect(), () => HasTarget).ObservesProperty(() => HasTarget);
			CloseCmd = new DelegateCommand<Object>(x => DoClose(x));

			AddressList = model_.SearchIP();
			if (AddressList.Count > 0) {
				connectIP_ = AddressList[0];
			}

			model_.PropertyChanged += ChangedProperty;
			cmds_.CloseCommand.RegisterCommand(CloseCmd);

		}

		public void Dispose() {
			model_.PropertyChanged -= ChangedProperty;
			cmds_.CloseCommand.UnregisterCommand(CloseCmd);
		}

		private void DoClose(Object target) {
			if (model_.IsConnected) {
				// 接続を解除する
				model_.ClickConnect(connectIP_);
			}
		}

		#region 下部ステータス表示関連の連携情報
		/// <summary>
		/// ノズルの温度情報
		/// </summary>
		public string NozelTemp {
			get => (!model_.IsConnected)? "" : String.Format("{0,3}/{1,3}", model_.CurrentTempNozel, model_.TargetTempNozel);
		}
		/// <summary>
		/// ベッドの温度状況
		/// </summary>
		public string BedTemp {
			get => (!model_.IsConnected) ? "" : String.Format("{0,3}/{1,3}", model_.CurrentTempBed, model_.TargetTempBed);
		}
		/// <summary>
		/// 機器のステータス情報
		/// </summary>
		public string TargetStatus {
			get => (!model_.IsConnected) ? Properties.Resources.MsgUnConnect : model_.Status.ToString();
		}
		#endregion

		/// <summary>
		/// 確認ダイアログ用(接続ボタン押下時に利用している)
		/// </summary>
		public InteractionRequest<INotification> ConnectDialog { get; private set; } = new InteractionRequest<INotification>();

		/// <summary>
		/// 接続ボタンが押された時の動作
		/// 未接続の場合は、接続をする。接続中の場合は、接続解除をする。
		/// </summary>
		private void ClickConnect() {
			if (model_.IsConnected) {
				// 接続中なので、接続解除がされる
				if (cmds_.DisConnectCommand.CanExecute(null)) {
					cmds_.DisConnectCommand.Execute(null);
				}
				else {
					// 接続解除をしない
					return;
				}
			}
			if (!model_.ClickConnect(connectIP_)) {
				// 失敗
				if (!model_.IsConnected) {
					// ユーザー確認ダイアログの表示
					ConnectDialog.Raise(new Notification { Title = Properties.Resources.Title, Content = Properties.Resources.MsgCantConnect });
					return;
				}
			}
			RaisePropertyChanged(nameof(NozelTemp));
			RaisePropertyChanged(nameof(BedTemp));
			RaisePropertyChanged(nameof(TargetStatus));
			RaisePropertyChanged(nameof(ConnectLabel));
		}

		/// <summary>
		/// 接続ボタンのラベルの内容
		/// 接続可能な場合は、「接続」、接続している場合は「接続解除」と表示する
		/// </summary>
		public string ConnectLabel {
			get => (!model_.IsConnected) ? PrintControler.Properties.Resources.BtnConnect : PrintControler.Properties.Resources.BtnDisConnect;
		}

		/// <summary>
		/// modelのプロパティ変更対応のイベント処理
		/// </summary>
		/// <param name="sender">オブジェクト名</param>
		/// <param name="eventArgs">イベント情報</param>
		private void ChangedProperty(object sender, PropertyChangedEventArgs eventArgs) {
			switch (eventArgs.PropertyName) {
			case Adventurer3Property.status:			// 機器のステータス更新がされたので、表示用のステータスを更新対象とする
				RaisePropertyChanged(nameof(NozelTemp));
				RaisePropertyChanged(nameof(BedTemp));
				RaisePropertyChanged(nameof(TargetStatus));
				break;
			case Adventurer3Property.connect:
				RaisePropertyChanged(nameof(ConnectLabel));
				break;
			}
		}
	}
}
