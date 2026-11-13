using Cysharp.Threading.Tasks;
using Nox.Assets;

namespace Nox.Worlds {
	public interface IWorldModule : IAssetModule {
		public UniTask<bool> Setup(IRuntimeWorld runtime);
	}
}
