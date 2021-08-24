using System;
using System.Runtime.CompilerServices;

namespace Take4.Common {
	/// <summary>
	/// ViewBaseと連携するためのモデルで利用するためのインターフェースクラス
	/// </summary>
	public interface ICheckableData {
		/// <summary>
		/// クラス内のデータが問題ないかどうかをチェックする
		/// </summary>
		/// <returns></returns>
		System.Collections.Generic.Dictionary<string, string> IsValid();

		/// <summary>
		/// ファイルからデータを読み込んだ場合に、読み込んだデータをオブジェクト内のデータに設定
		/// </summary>
		/// <param name="data"></param>
		void set(ICheckableData data);
	}

	/// <summary>
	/// ViewModelの基本的な動作部分をまとめたもの
	/// </summary>
	/// <typeparam name="Data">Model相当のクラス</typeparam>
	public class ViewBase<Data> : Prism.Mvvm.BindableBase, System.ComponentModel.INotifyDataErrorInfo where Data : ICheckableData {
		/// <summary>
		/// モデル相当のデータ
		/// </summary>
		protected Data parameter_;

		/// <summary>
		/// 保存用のパラメータファイル名
		/// </summary>
		private string parameterFileName_ = "";

		protected string ParameterFileName {
			get => parameterFileName_;
			set => parameterFileName_ = value;
		}

		protected ViewBase() {
			ErrorsContainer = new Prism.Mvvm.ErrorsContainer<string>(
				x => ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(x)));
		}

		/// <summary>
		/// データの設定用メソッド
		/// データの比較を行い、変更があった場合、parameter側の属性を変更し、parameterのチェック処理を実施する
		/// </summary>
		protected void SetProperty<T>(T value, [CallerMemberName] string propertyName = null) where T : IComparable {
			System.Reflection.PropertyInfo property = parameter_.GetType().GetProperty(propertyName);
			if (property == null || (!property.CanRead && !property.CanWrite)) {
				return;
			}
			var target = (property.GetValue(parameter_)) as IComparable;
			if (target != null && value.CompareTo(target) != 0) {
				property.SetValue(parameter_, value);
				RaisePropertyChanged(propertyName);
				Check();
			}
		}

		/// <summary>
		/// パラメータのチェックを行い、エラーがある場合、エラーコンテナに詰め込む
		/// これにより、エラーがあったテキストが赤くなり、エラー情報がポップアップで表示される
		/// </summary>
		protected void Check() {
			var result = parameter_.IsValid();
			ErrorsContainer.ClearErrors();
			foreach (var data in result) {
				System.Collections.Generic.List<string> work = new System.Collections.Generic.List<string>();
				work.Add(data.Value);
				ErrorsContainer.SetErrors(data.Key, work);
				RaisePropertyChanged(data.Key);
			}
		}

		#region パラメータのファイルからの入出力
		/// <summary>
		/// ファイル読み込み後、通知を出すプロパティの一覧を入れておく
		/// </summary>
		protected virtual void RaisePropertyAfterLoadParameter() {
		}

		/// <summary>
		/// パラメータファイルから設定を読み込む
		/// </summary>
		/// <param name="filename">パラメータファイル名</param>
		public void LoadParameter(string filename = null) {
			if (filename == null) {
				filename = parameterFileName_;
			}
			if (System.IO.File.Exists(filename)) {
				using (var wStream = System.IO.File.OpenRead(filename)) {
					var settings = new System.Runtime.Serialization.Json.DataContractJsonSerializerSettings();
					var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Data), settings);
					var data1 = (Data)serializer.ReadObject(wStream);
					parameter_.set(data1);
					RaisePropertyAfterLoadParameter();
					Check();
				}
			}
		}

		/// <summary>
		/// パラメータファイルに現在の設定を書き込む
		/// </summary>
		/// <param name="filename">パラメータファイル名</param>
		public void SaveParameter(string filename = null) {
			if (filename == null) {
				filename = parameterFileName_;
			}
			using (var stream1 = System.IO.File.Create(filename)) {
				var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Data));
				serializer.WriteObject(stream1, parameter_);
			}
		}
		#endregion

		#region エラー対応用
		public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs> ErrorsChanged;

		protected Prism.Mvvm.ErrorsContainer<string> ErrorsContainer { get; }

		public bool HasErrors {
			get => ErrorsContainer.HasErrors;
		}

		public System.Collections.IEnumerable GetErrors(string propertyName) {
			return this.ErrorsContainer.GetErrors(propertyName);
		}
		#endregion
	}
}
