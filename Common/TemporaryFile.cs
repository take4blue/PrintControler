using System;
using System.IO;

namespace Take4.Common {
	/// <summary>
	/// 作業用ファイルを管理するためのクラス
	/// </summary>
	public class TemporaryFile : IDisposable {
		string fileName_;

		/// <summary>
		/// 作業用ファイル名の取り出し
		/// </summary>
		/// <remarks>ファイル名がない場合、内部でCreateを呼び出す</remarks>
		public string FileName {
			get {
				if (string.IsNullOrEmpty(fileName_)) {
					Create();
				}
				return fileName_;
			}
		}

		/// <summary>
		/// 作業用ファイルの削除
		/// </summary>
		public void Delete() {
			if (!string.IsNullOrEmpty(fileName_) && File.Exists(fileName_)) {
				File.Delete(fileName_);
			}
			fileName_ = null;
		}

		/// <summary>
		/// 作業用ファイル名の作成
		/// </summary>
		/// <remarks>作業用ファイルは作成される</remarks>
		public void Create() {
			Delete();
			fileName_ = Path.GetTempFileName();
		}

		public void Dispose() {
			Delete();
		}
	}
}
