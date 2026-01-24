using Cysharp.Threading.Tasks;

namespace Nox.Worlds {
	public interface IWorldModule {
		public UniTask<bool> Setup(IRuntimeWorld runtime);
	}
}