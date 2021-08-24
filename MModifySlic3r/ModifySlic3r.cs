using System.Collections.Generic;
using System.Runtime.Serialization;
using Take4.Common;
using Take4.Translator;

namespace PrintControler.MModifySlic3r {
	/// <summary>
	/// Scli3rのファイルを、MModify側に渡せる状態になるまで加工する
	/// </summary>
	[DataContract]
	public class ModifySlic3r : Slic3rToBase, ICheckableData {
		public Dictionary<string, string> IsValid() {
			System.Collections.Generic.Dictionary<string, string> result = new System.Collections.Generic.Dictionary<string, string>();
			Take4.Common.Checker.RangeCheck(SpeedZ, 1, 3000, ref result, nameof(SpeedZ));
			return result;
		}

		public void set(ICheckableData data) {
			var value = data as ModifySlic3r;
			if (value != null) {
				SpeedZ = value.SpeedZ;
			}
		}
	}
}
